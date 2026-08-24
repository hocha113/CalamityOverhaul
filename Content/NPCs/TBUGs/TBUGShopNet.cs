using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.TBUGs
{
    internal enum TBUGShopOp : byte
    {
        PurchaseRequest,
        PurchaseResult,
        BestiaryChat,
        /// <summary>服务端 → 全体：全量库存表（补货沿广播；入世快照走 TBUGStockSystem.NetSend）</summary>
        StockSync,
        /// <summary>服务端 → 全体：单件余量（成交/退货后广播绝对值）</summary>
        StockDelta,
        /// <summary>客户端 → 服务端：本机结算失败，退回一件库存</summary>
        SettleRollback,
    }

    internal enum TBUGShopResult : byte
    {
        Success,
        InvalidRequest,
        OutOfRange,
        InsufficientFunds,
        InventoryFull,
        OutOfStock,
        Busy,
        /// <summary>仅客户端本地：回执超时</summary>
        Timeout,
    }

    /// <summary>
    /// 黑客商店购买通道。铁律：服务端只过审并定价，钱货两清由请求方本机结算
    /// 非 SSC 下服务端写不了客户端自己的背包与钱包（参照 CyberwareLocalSettlement）。
    /// NPC 用裸下标寻址，服务端对解析出的 NPC 复验存活/类型/距离
    /// </summary>
    internal static class TBUGShopNet
    {
        /// <summary>互动最大距离 px，服务端复验</summary>
        private const float MaxInteractDistance = 500f;
        /// <summary>挂起回执超时帧</summary>
        private const int PendingTimeoutFrames = 150;
        /// <summary>服务端同玩家两次请求最小间隔帧</summary>
        private const uint ServerRateLimitFrames = 8;

        private readonly record struct PendingPurchase(uint Serial, int ItemType,
            uint ExpireFrame, Action<TBUGShopResult, long> Callback);

        //本机挂起表：客户端本地状态（不是服务端 per-player 状态，static 无碍）
        private static readonly List<PendingPurchase> pending = [];
        private static uint serialCounter;

        //服务端限频戳：whoAmI → 上次请求帧（per-player 状态放字典，键即玩家）
        private static readonly Dictionary<int, uint> lastRequestFrame = [];

        private static ModPacket NewPacket(TBUGShopOp op) {
            ModPacket packet = CWRMod.Instance.GetPacket();
            packet.Write((byte)CWRMessageType.TBUGShop);
            packet.Write((byte)op);
            return packet;
        }

        #region 购买

        /// <summary>
        /// 本机发起购买；单机直接走同一套校验与结算，多人发包等回执。
        /// 返回 false 表示请求根本没发出去（调用方应立刻解除挂起态）
        /// </summary>
        internal static bool SendPurchaseRequest(Player player, int tbugWhoAmI,
            int itemType, Action<TBUGShopResult, long> completion) {
            if (Main.netMode == NetmodeID.Server || player?.active != true
                || player.dead || player.whoAmI != Main.myPlayer
                || itemType <= ItemID.None || itemType >= ItemLoader.ItemCount
                || tbugWhoAmI < 0 || tbugWhoAmI >= Main.maxNPCs) {
                return false;
            }

            if (Main.netMode == NetmodeID.SinglePlayer) {
                TBUGShopResult code = ValidatePurchase(player, tbugWhoAmI, itemType, out long price);
                if (code == TBUGShopResult.Success) {
                    code = SettlePurchase(player, itemType, price);
                }
                if (code == TBUGShopResult.Success) {
                    //单机权威就在本机：钱货两清后才扣库存
                    TBUGStock.Consume(itemType);
                }
                completion?.Invoke(code, price);
                return true;
            }

            uint serial = ++serialCounter;
            pending.Add(new PendingPurchase(serial, itemType,
                (uint)(Main.GameUpdateCount + PendingTimeoutFrames), completion));

            ModPacket packet = NewPacket(TBUGShopOp.PurchaseRequest);
            packet.Write((short)tbugWhoAmI);
            packet.Write(itemType);
            packet.Write(serial);
            packet.Send();
            return true;
        }

        /// <summary>
        /// 过审：单机与服务端共用同一份判据（谓词单一来源），别复制粘贴出第二份
        /// </summary>
        private static TBUGShopResult ValidatePurchase(Player player, int tbugWhoAmI,
            int itemType, out long price) {
            price = 0L;
            if (tbugWhoAmI < 0 || tbugWhoAmI >= Main.maxNPCs) {
                return TBUGShopResult.InvalidRequest;
            }
            NPC tbug = Main.npc[tbugWhoAmI];
            if (tbug?.active != true || tbug.type != ModContent.NPCType<TBUG>()) {
                return TBUGShopResult.InvalidRequest;
            }
            if (player.Distance(tbug.Center) > MaxInteractDistance) {
                return TBUGShopResult.OutOfRange;
            }
            if (!TBUGCatalog.TryGetEntry(itemType, out TBUGCatalogEntry entry)
                || entry.Price <= 0L) {
                return TBUGShopResult.InvalidRequest;
            }
            if (TBUGStock.GetStock(itemType) <= 0) {
                return TBUGShopResult.OutOfStock;
            }
            price = TBUGCatalog.GetAuthorityPrice(itemType, player, tbug);
            if (price <= 0L) {
                return TBUGShopResult.InvalidRequest;
            }
            if (!player.CanAfford(price)) {
                return TBUGShopResult.InsufficientFunds;
            }
            if (FindEmptyMainSlot(player) < 0) {
                return TBUGShopResult.InventoryFull;
            }
            return TBUGShopResult.Success;
        }

        /// <summary>本机结算：占位再扣款（找零会挑空格落脚，先占住目标格免得货被顶掉）</summary>
        private static TBUGShopResult SettlePurchase(Player player, int itemType, long price) {
            if (itemType <= ItemID.None || itemType >= ItemLoader.ItemCount || price <= 0L) {
                return TBUGShopResult.InvalidRequest;
            }
            if (!player.CanAfford(price)) {
                return TBUGShopResult.InsufficientFunds;
            }
            int destination = FindEmptyMainSlot(player);
            if (destination < 0) {
                return TBUGShopResult.InventoryFull;
            }
            Item purchased = new(itemType);
            if (purchased.IsAir) {
                return TBUGShopResult.InvalidRequest;
            }

            player.inventory[destination] = purchased;
            bool paid;
            try {
                paid = player.BuyItem(price);
            } catch (Exception ex) {
                paid = false;
                CWRMod.Instance.Logger.Error($"TBUG purchase payment failed: {ex.Message}");
            }
            if (!paid) {
                //CanAfford 已经过了，这里只会是找零无处安放
                player.inventory[destination] = new Item();
                return TBUGShopResult.InventoryFull;
            }

            //首单台词桶
            TBUGDialogue.NoteFirstPurchase();
            return TBUGShopResult.Success;
        }

        /// <summary>主背包（含快捷栏）第一个空格，不碰钱币/弹药格</summary>
        private static int FindEmptyMainSlot(Player player) {
            for (int i = 0; i < 50; i++) {
                Item item = player.inventory[i];
                if (item == null || item.IsAir) {
                    return i;
                }
            }
            return -1;
        }

        private static void HandlePurchaseRequest(BinaryReader reader, int whoAmI) {
            //链式共享 reader：先读完全部载荷再做任何校验早退
            int tbugWho = reader.ReadInt16();
            int itemType = reader.ReadInt32();
            uint serial = reader.ReadUInt32();

            if (Main.netMode != NetmodeID.Server) {
                return;
            }

            Player player = whoAmI >= 0 && whoAmI < Main.maxPlayers ? Main.player[whoAmI] : null;
            TBUGShopResult code;
            long price = 0L;

            if (player?.active != true || player.dead) {
                code = TBUGShopResult.InvalidRequest;
            }
            else if (lastRequestFrame.TryGetValue(whoAmI, out uint last)
                && Main.GameUpdateCount - last < ServerRateLimitFrames) {
                code = TBUGShopResult.Busy;
            }
            else {
                lastRequestFrame[whoAmI] = (uint)Main.GameUpdateCount;
                code = ValidatePurchase(player, tbugWho, itemType, out price);
            }

            //拒绝必须能被诊断：留一行日志说明是哪条判据打回的
            if (code != TBUGShopResult.Success) {
                CWRMod.Instance.Logger.Info(
                    $"TBUG purchase rejected ({code}): player={whoAmI} npc={tbugWho} item={itemType}");
            }
            else {
                //过审即占货：钱货两清在客户端，客户端结算失败会发 SettleRollback 退回
                TBUGStock.Consume(itemType);
                BroadcastStockDelta(itemType);
            }

            ModPacket reply = NewPacket(TBUGShopOp.PurchaseResult);
            reply.Write(serial);
            reply.Write((byte)code);
            reply.Write(price);
            reply.Write(itemType);
            reply.Send(toClient: whoAmI);
        }

        private static void HandlePurchaseResult(BinaryReader reader) {
            uint serial = reader.ReadUInt32();
            byte codeByte = reader.ReadByte();
            long price = reader.ReadInt64();
            int itemType = reader.ReadInt32();

            if (Main.netMode != NetmodeID.MultiplayerClient) {
                return;
            }

            int index = pending.FindIndex(p => p.Serial == serial);
            if (index < 0) {
                //已超时清账的迟到回执：钱货都没动过，安全丢弃
                return;
            }
            PendingPurchase entry = pending[index];
            pending.RemoveAt(index);

            TBUGShopResult code = codeByte <= (byte)TBUGShopResult.Timeout
                ? (TBUGShopResult)codeByte
                : TBUGShopResult.InvalidRequest;
            if (code == TBUGShopResult.Success) {
                //权威只回价，钱货两清在本机
                code = SettlePurchase(Main.LocalPlayer, itemType, price);
                if (code != TBUGShopResult.Success) {
                    //服务端过审时已占货，本机没结算成就退回去
                    ModPacket rollback = NewPacket(TBUGShopOp.SettleRollback);
                    rollback.Write(itemType);
                    rollback.Send();
                }
            }
            entry.Callback?.Invoke(code, price);
        }

        /// <summary>客户端逐帧清挂起超时（由 <see cref="TBUGShopNetSystem"/> 驱动）</summary>
        internal static void UpdatePending() {
            if (pending.Count == 0) {
                return;
            }
            for (int i = pending.Count - 1; i >= 0; i--) {
                if (Main.GameUpdateCount < pending[i].ExpireFrame) {
                    continue;
                }
                PendingPurchase entry = pending[i];
                pending.RemoveAt(i);
                entry.Callback?.Invoke(TBUGShopResult.Timeout, 0L);
            }
        }

        internal static void ClearPending() {
            pending.Clear();
            lastRequestFrame.Clear();
        }

        #endregion

        #region 库存同步

        /// <summary>补货沿广播全量库存表；单机/客户端调用是空操作</summary>
        internal static void BroadcastStockSync() {
            if (Main.netMode != NetmodeID.Server) {
                return;
            }
            ModPacket packet = NewPacket(TBUGShopOp.StockSync);
            packet.Write(TBUGStock.RestockEpoch);
            IReadOnlyDictionary<int, int> export = TBUGStock.Export();
            packet.Write((short)export.Count);
            foreach ((int itemType, int count) in export) {
                packet.Write(itemType);
                packet.Write((short)count);
            }
            packet.Send();
        }

        /// <summary>成交/退货后广播单件余量绝对值</summary>
        private static void BroadcastStockDelta(int itemType) {
            if (Main.netMode != NetmodeID.Server) {
                return;
            }
            ModPacket packet = NewPacket(TBUGShopOp.StockDelta);
            packet.Write(itemType);
            packet.Write((short)TBUGStock.GetStock(itemType));
            packet.Send();
        }

        private static void HandleStockSync(BinaryReader reader) {
            //链式共享 reader：先读完全部载荷再做任何校验早退
            int epoch = reader.ReadInt32();
            int count = reader.ReadInt16();
            List<(int, int)> entries = new(Math.Max(0, count));
            for (int i = 0; i < count; i++) {
                entries.Add((reader.ReadInt32(), reader.ReadInt16()));
            }
            if (Main.netMode != NetmodeID.MultiplayerClient) {
                return;
            }
            TBUGStock.ApplyNet(epoch, entries);
        }

        private static void HandleStockDelta(BinaryReader reader) {
            int itemType = reader.ReadInt32();
            int count = reader.ReadInt16();
            if (Main.netMode != NetmodeID.MultiplayerClient) {
                return;
            }
            TBUGStock.SetStock(itemType, count);
        }

        private static void HandleSettleRollback(BinaryReader reader, int whoAmI) {
            int itemType = reader.ReadInt32();
            if (Main.netMode != NetmodeID.Server) {
                return;
            }
            //退货同样要可诊断：正常玩法里它只在"过审后背包又满了"这类边角出现
            CWRMod.Instance.Logger.Info($"TBUG purchase rolled back: player={whoAmI} item={itemType}");
            TBUGStock.Refund(itemType);
            BroadcastStockDelta(itemType);
        }

        #endregion

        #region 图鉴

        /// <summary>
        /// 客户端请求服务端登记图鉴交谈记录；服务端登记后经原版 NetBestiaryModule 广播
        /// </summary>
        internal static void SendBestiaryChat(NPC tbug) {
            if (Main.netMode != NetmodeID.MultiplayerClient || tbug?.active != true) {
                return;
            }
            ModPacket packet = NewPacket(TBUGShopOp.BestiaryChat);
            packet.Write((short)tbug.whoAmI);
            packet.Send();
        }

        private static void HandleBestiaryChat(BinaryReader reader) {
            int who = reader.ReadInt16();
            if (Main.netMode != NetmodeID.Server) {
                return;
            }
            if (who < 0 || who >= Main.maxNPCs) {
                return;
            }
            NPC npc = Main.npc[who];
            if (npc?.active != true || npc.type != ModContent.NPCType<TBUG>()) {
                return;
            }
            Main.BestiaryTracker.Chats.RegisterChatStartWith(npc);
        }

        #endregion

        internal static void NetHandle(CWRMessageType type, BinaryReader reader, int whoAmI) {
            if (type != CWRMessageType.TBUGShop) {
                return;
            }
            try {
                TBUGShopOp op = (TBUGShopOp)reader.ReadByte();
                switch (op) {
                    case TBUGShopOp.PurchaseRequest:
                        HandlePurchaseRequest(reader, whoAmI);
                        break;
                    case TBUGShopOp.PurchaseResult:
                        HandlePurchaseResult(reader);
                        break;
                    case TBUGShopOp.BestiaryChat:
                        HandleBestiaryChat(reader);
                        break;
                    case TBUGShopOp.StockSync:
                        HandleStockSync(reader);
                        break;
                    case TBUGShopOp.StockDelta:
                        HandleStockDelta(reader);
                        break;
                    case TBUGShopOp.SettleRollback:
                        HandleSettleRollback(reader, whoAmI);
                        break;
                }
            } catch (EndOfStreamException) {
            } catch (IOException) {
            }
        }
    }

    /// <summary>客户端挂起超时清账；服务端限频表随世界卸载清空</summary>
    internal class TBUGShopNetSystem : ModSystem
    {
        public override void PostUpdateEverything() {
            if (!Main.dedServ) {
                TBUGShopNet.UpdatePending();
            }
        }

        public override void OnWorldUnload() {
            TBUGShopNet.ClearPending();
            TBUGSession.Clear();
            TBUGMood.Invalidate();
            TBUGDialogue.ResetSession();
        }
    }
}

using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Industrials.MaterialFlow.Batterys;
using InnoVault.TileProcessors;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.SlimeVats
{
    /// <summary>
    /// 史莱姆培养槽TP:水+电周期性培养凝胶,生物质发电机的燃料源头。<br/>
    /// 供水走无依赖设计:自动汲取机身邻接/下方的世界水体(权威端+原版液体同步),
    /// 或 UI 手动倒水桶;内部水缓冲 4 格(1020 单位,255=1格)。<br/>
    /// TODO(液体管道对接):液体管道网(Direction A,另一批次施工)落地后,
    /// 把 <see cref="WaterStored"/>/<see cref="WaterCapacity"/> 暴露为其 IFluidContainer
    /// (FluidType=LiquidID.Water)即可入网;本机数据语义已按 255单位=1格 对齐,无需改动结算
    /// </summary>
    internal class SlimeVatTP : BaseBattery
    {
        public override int TargetTileID => ModContent.TileType<SlimeVatTile>();
        public override int TargetItem => ModContent.ItemType<SlimeVat>();
        public override bool ReceivedEnergy => true;
        public override bool CanDrop => false;
        public override float MaxUEValue => 500;
        /// <summary>全量包携带4格物品数据,放宽锚定节奏</summary>
        public override int NetAnchorIntervalTicks => 600;

        #region 常量

        internal const int ProduceSlotCount = 4;
        /// <summary>一轮培养的电力开销</summary>
        internal const float BrewCost = 8f;
        /// <summary>一轮培养的水耗(单位,255=1格)</summary>
        internal const int WaterCost = 255;
        /// <summary>一轮培养的凝胶产量</summary>
        internal const int GelPerCycle = 3;
        /// <summary>培养周期(tick),30秒一轮</summary>
        internal const int CycleTicks = 1800;
        /// <summary>内部水缓冲上限:4格</summary>
        internal const int WaterCapacity = 1020;
        //自动汲水节拍与扫描外扩(格)
        private const int PumpInterval = 30;
        private const int PumpExpand = 2;
        private const int PumpDepth = 4;
        //账本合批同步节流
        private const int NetInterval = 30;

        #endregion

        #region 字段

        internal Item[] Produce = new Item[ProduceSlotCount];
        internal bool Enabled = true;

        /// <summary>内部水缓冲(单位)</summary>
        internal int WaterStored;
        /// <summary>培养进度(tick 计),UI 进度条用</summary>
        internal float BrewProgress;

        internal bool IsWorking { get; private set; }
        internal float GlowIntensity;

        private int pumpTimer;
        private int textIdleTime;
        private byte brewRevision;
        private bool netDirty;
        private int netCooldown;

        #endregion

        #region 属性

        internal bool ProduceHasSpace {
            get {
                foreach (Item item in Produce) {
                    if (item == null || item.IsAir) {
                        return true;
                    }
                    if (item.type == ItemID.Gel && item.stack < item.maxStack) {
                        return true;
                    }
                }
                return false;
            }
        }

        #endregion

        public override void SetBattery() {
            EnsureSlots();
        }

        public override void Initialize() {
            EnsureSlots();
        }

        private void EnsureSlots() {
            Produce ??= new Item[ProduceSlotCount];
            for (int i = 0; i < ProduceSlotCount; i++) {
                Produce[i] ??= new Item();
            }
        }

        #region 数据同步与存档

        public override void SendData(ModPacket data) {
            base.SendData(data);
            data.Write(Enabled);
            data.Write(WaterStored);
            data.Write(BrewProgress);
            data.Write(brewRevision);
            for (int i = 0; i < ProduceSlotCount; i++) {
                ItemIO.Send(Produce[i] ?? new Item(), data, true);
            }
        }

        public override void ReceiveData(BinaryReader reader, int whoAmI) {
            base.ReceiveData(reader, whoAmI);
            EnsureSlots();
            Enabled = reader.ReadBoolean();
            WaterStored = reader.ReadInt32();
            BrewProgress = reader.ReadSingle();
            byte newRevision = reader.ReadByte();
            for (int i = 0; i < ProduceSlotCount; i++) {
                Produce[i] = ItemIO.Receive(reader, true);
            }

            //修订号推进才播培养演出,入世快照不播
            if (!TileProcessorNetWork.InitializeWorld && newRevision != brewRevision) {
                PlayBrewEffect();
            }
            brewRevision = newRevision;
        }

        public override void SaveData(TagCompound tag) {
            base.SaveData(tag);
            try {
                tag["_Enabled"] = Enabled;
                tag["_WaterStored"] = WaterStored;
                tag["_BrewProgress"] = BrewProgress;
                List<TagCompound> produceTags = [];
                for (int i = 0; i < ProduceSlotCount; i++) {
                    produceTags.Add(ItemIO.Save(Produce[i] ?? new Item()));
                }
                tag["_Produce"] = produceTags;
            } catch (Exception ex) {
                VaultMod.Instance.Logger.Error($"SlimeVatTP.SaveData Error: {ex.Message}");
            }
        }

        public override void LoadData(TagCompound tag) {
            base.LoadData(tag);
            try {
                EnsureSlots();
                if (tag.TryGet("_Enabled", out bool enabled)) {
                    Enabled = enabled;
                }
                if (tag.TryGet("_WaterStored", out int water)) {
                    WaterStored = Math.Clamp(water, 0, WaterCapacity);
                }
                if (tag.TryGet("_BrewProgress", out float progress)) {
                    BrewProgress = Math.Clamp(progress, 0f, CycleTicks);
                }
                if (tag.TryGet("_Produce", out List<TagCompound> produceTags)) {
                    for (int i = 0; i < ProduceSlotCount && i < produceTags.Count; i++) {
                        Produce[i] = CWRSaveData.LoadItemTag(produceTags[i], $"{nameof(SlimeVatTP)}:_Produce");
                    }
                }
            } catch (Exception ex) {
                VaultMod.Instance.Logger.Error($"SlimeVatTP.LoadData Error: {ex.Message}");
            }
        }

        /// <summary>槽位/水量被UI/管道改动后调用:权威端下次节流推送合并纠偏</summary>
        internal void MarkDirty() => netDirty = true;

        #endregion

        #region 汲水

        /// <summary>
        /// 自动汲取机身邻接与下方的世界水体:一拍抽一格,液体清除是世界改动,
        /// 权威端主线程执行后走原版液体同步
        /// </summary>
        private void TryPumpWater() {
            if (WaterStored > WaterCapacity - 255) {
                return;
            }

            int tileWidth = Width / 16;
            int tileHeight = Height / 16;
            int left = Position.X - PumpExpand;
            int right = Position.X + tileWidth + PumpExpand - 1;
            int top = Position.Y;
            int bottom = Position.Y + tileHeight + PumpDepth - 1;

            //先扫后抽:扫描只读,并行阶段安全;实际清水进主线程闭包
            Point16 found = Point16.Zero;
            for (int y = top; y <= bottom && found == Point16.Zero; y++) {
                for (int x = left; x <= right; x++) {
                    if (!WorldGen.InWorld(x, y, 5)) {
                        continue;
                    }
                    Tile tile = Main.tile[x, y];
                    if (tile.LiquidAmount <= 0 || tile.LiquidType != LiquidID.Water) {
                        continue;
                    }
                    if (tile.HasTile && Main.tileSolid[tile.TileType]) {
                        continue;
                    }
                    found = new Point16(x, y);
                    break;
                }
            }

            if (found == Point16.Zero) {
                return;
            }

            Defer(() => {
                if (WaterStored > WaterCapacity - 255) {
                    return;
                }
                Tile tile = Main.tile[found.X, found.Y];
                if (tile.LiquidAmount <= 0 || tile.LiquidType != LiquidID.Water) {
                    return;
                }

                WaterStored = Math.Min(WaterCapacity, WaterStored + tile.LiquidAmount);
                tile.LiquidAmount = 0;
                WorldGen.SquareTileFrame(found.X, found.Y, false);
                if (VaultUtils.isServer) {
                    NetMessage.sendWater(found.X, found.Y);
                }
                netDirty = true;
            });
        }

        /// <summary>UI 倒水桶:普通水桶+255并退还空桶,无底水桶白给;客户端权威编辑,调用方负责推送</summary>
        internal bool TryPourBucket(Item bucket) {
            if (bucket == null || bucket.IsAir || WaterStored > WaterCapacity - 255) {
                return false;
            }

            if (bucket.type == ItemID.BottomlessBucket) {
                WaterStored = Math.Min(WaterCapacity, WaterStored + 255);
                return true;
            }

            if (bucket.type == ItemID.WaterBucket) {
                bucket.stack--;
                if (bucket.stack <= 0) {
                    bucket.TurnToAir();
                }
                Main.LocalPlayer.GiveItem(new EntitySource_WorldEvent(), new Item(ItemID.EmptyBucket));
                WaterStored = Math.Min(WaterCapacity, WaterStored + 255);
                return true;
            }

            return false;
        }

        #endregion

        #region 更新逻辑

        public override void UpdateMachine() {
            //权威端节流刷新
            if (netCooldown > 0) {
                netCooldown--;
            }
            if (netDirty && netCooldown <= 0 && VaultUtils.isServer) {
                netDirty = false;
                netCooldown = NetInterval;
                SendData();
            }
            if (textIdleTime > 0) {
                textIdleTime--;
            }

            GlowIntensity = IsWorking
                ? Math.Min(1f, GlowIntensity + 0.04f)
                : Math.Max(0f, GlowIntensity - 0.02f);

            if (!Enabled) {
                IsWorking = false;
                return;
            }

            bool authority = !VaultUtils.isClient;
            if (!authority) {
                return;
            }

            //自动汲水
            if (++pumpTimer >= PumpInterval) {
                pumpTimer = 0;
                TryPumpWater();
            }

            //原料齐备才推进培养进度:缺水/缺电/满仓时培养液休眠
            bool canWork = WaterStored >= WaterCost && ProduceHasSpace && MachineData.UEvalue >= BrewCost;
            if (!canWork) {
                IsWorking = false;
                if (WaterStored < WaterCost) {
                    Prompt(SlimeVat.NoWaterText.Value);
                }
                else if (!ProduceHasSpace) {
                    Prompt(SlimeVat.FullText.Value);
                }
                else {
                    Prompt(SlimeVat.NoEnergyText.Value);
                }
                return;
            }

            IsWorking = true;
            BrewProgress += 1f;
            if (BrewProgress < CycleTicks) {
                return;
            }

            BrewProgress = 0f;
            netDirty = true;

            //结算在主线程做,与UI编辑无竞争
            Defer(() => {
                if (MachineData.UEvalue < BrewCost || WaterStored < WaterCost || !ProduceHasSpace) {
                    return;
                }

                WaterStored -= WaterCost;
                MachineData.UEvalue -= BrewCost;

                int remain = InsertItem(Produce, ItemID.Gel, GelPerCycle);
                if (remain > 0) {
                    DropItem(new Item(ItemID.Gel, remain));
                }

                brewRevision++;
                netDirty = true;

                PlayBrewEffect();
            });
        }

        private static int InsertItem(Item[] slots, int itemType, int count) {
            //先叠同类
            foreach (Item slot in slots) {
                if (count <= 0) {
                    return 0;
                }
                if (slot == null || slot.IsAir || slot.type != itemType || slot.stack >= slot.maxStack) {
                    continue;
                }
                int add = Math.Min(count, slot.maxStack - slot.stack);
                slot.stack += add;
                count -= add;
            }
            //再开新槽
            for (int i = 0; i < slots.Length && count > 0; i++) {
                if (slots[i] != null && !slots[i].IsAir) {
                    continue;
                }
                slots[i] = new Item(itemType, count);
                count = 0;
            }
            return count;
        }

        private void Prompt(string text) {
            if (textIdleTime > 0) {
                return;
            }
            textIdleTime = 300;
            //并行阶段CombatText生成延迟到主线程执行(串行阶段立即执行)
            Defer(() => CombatText.NewText(HitBox, SlimeVat.Tint, text));
        }

        /// <summary>培养演出:槽口涌出凝胶泡;主线程调用,服务器跳过</summary>
        internal void PlayBrewEffect() {
            if (VaultUtils.isServer) {
                return;
            }

            Vector2 mouth = CenterInWorld + new Vector2(0, -8);
            for (int i = 0; i < 16; i++) {
                Dust dust = Dust.NewDustPerfect(mouth + Main.rand.NextVector2Circular(12f, 6f),
                    DustID.t_Slime, new Vector2(Main.rand.NextFloat(-1.5f, 1.5f), -Main.rand.NextFloat(0.8f, 2.6f)),
                    120, new Color(78, 200, 120), 1.2f);
                dust.noGravity = false;
            }
            SoundEngine.PlaySound(SoundID.NPCDeath1 with { Volume = 0.35f, Pitch = 0.5f }, mouth);
        }

        #endregion

        #region 交互/销毁/绘制

        public void OpenUI() {
            var ui = UIHandleLoader.GetUIHandleOfType<SlimeVatUI>();
            ui?.Interactive(this);
        }

        public override void MachineKill() {
            if (VaultUtils.isClient) {
                return;
            }

            //倒出全部产出;缓冲里的水随拆机流失(与液体储罐的 v1 取舍一致)
            for (int i = 0; i < ProduceSlotCount; i++) {
                if (Produce[i] != null && !Produce[i].IsAir) {
                    VaultUtils.SpwanItem(this.FromObjectGetParent(), HitBox, Produce[i]);
                    Produce[i] = new Item();
                }
            }

            //掉落机器本身(带能量)
            Item vatItem = new Item(ModContent.ItemType<SlimeVat>());
            vatItem.CWR().UEValue = MachineData.UEvalue;
            int type = Item.NewItem(new EntitySource_WorldEvent(), HitBox, vatItem);
            if (VaultUtils.isServer) {
                NetMessage.SendData(MessageID.SyncItem, -1, -1, null, type);
            }
        }

        public override void FrontDraw(SpriteBatch spriteBatch) {
            DrawChargeBar();
        }

        #endregion
    }
}

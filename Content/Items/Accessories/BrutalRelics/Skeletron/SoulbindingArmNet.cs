using CalamityOverhaul.Content.TimeFreezes;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Accessories.BrutalRelics.Skeletron
{
    internal enum SoulbindingOp : byte
    {
        /// <summary>owner → 服务端 → 其余客户端：收魂（计数 + 起点，供远端收魂演出）</summary>
        Gain,
        /// <summary>owner → 服务端：请求灭杀被格挡的敌方弹幕</summary>
        BlockRequest,
        /// <summary>服务端 → 其余客户端：格挡结果转播（计数 + 爆点）</summary>
        BlockFx,
        /// <summary>owner → 服务端 → 其余客户端：魂数全量对账（出手清零 / 慢速纠偏）</summary>
        State,
    }

    /// <summary>
    /// 缚魂之腕信道。铁律：敌方弹幕（owner=255）只有服务端能权威灭杀并广播，
    /// 拥有者端只本地消隐求手感；魂数是拥有者自报的表现值，服务端只转播不裁决，
    /// 真正的裁决面（弹幕存活/距离/频率）由服务端复验
    /// </summary>
    internal class SoulbindingArmNet : CWRNetChannel
    {
        /// <summary>格挡复验距离（px），比客户端判定半径宽放余量</summary>
        private const float BlockValidateRange = 620f;

        //服务端限频戳：whoAmI → 上次请求帧（per-player 状态放字典，键即玩家）
        private static readonly Dictionary<int, uint> lastBlockFrame = [];

        private static ModPacket NewPacket(SoulbindingOp op) {
            ModPacket packet = CWRNetWork.GetPacket<SoulbindingArmNet>();
            packet.Write((byte)op);
            return packet;
        }

        #region 发送
        internal static void SendGain(int who, int count, Vector2 from) {
            if (Main.netMode != NetmodeID.MultiplayerClient) {
                return;
            }
            ModPacket packet = NewPacket(SoulbindingOp.Gain);
            packet.Write((byte)who);
            packet.Write((byte)count);
            packet.WriteVector2(from);
            packet.Send();
        }

        internal static void SendState(int who, int count) {
            if (Main.netMode != NetmodeID.MultiplayerClient) {
                return;
            }
            ModPacket packet = NewPacket(SoulbindingOp.State);
            packet.Write((byte)who);
            packet.Write((byte)count);
            packet.Send();
        }

        /// <summary>发送灭杀请求；须在本地消隐（active=false）之前调用，身份捕获要求弹幕存活</summary>
        internal static void SendBlockRequest(Projectile proj, int count, Vector2 pos) {
            if (Main.netMode != NetmodeID.MultiplayerClient) {
                return;
            }
            NetworkProjectileIdentity identity = NetworkProjectileIdentity.Capture(proj);
            ModPacket packet = NewPacket(SoulbindingOp.BlockRequest);
            identity.Write(packet);
            packet.Write((byte)count);
            packet.WriteVector2(pos);
            packet.Send();
        }
        #endregion

        #region 接收
        public override void Receive(BinaryReader reader, int whoAmI) {
            try {
                SoulbindingOp op = (SoulbindingOp)reader.ReadByte();
                switch (op) {
                    case SoulbindingOp.Gain:
                        HandleGain(reader, whoAmI);
                        break;
                    case SoulbindingOp.BlockRequest:
                        HandleBlockRequest(reader, whoAmI);
                        break;
                    case SoulbindingOp.BlockFx:
                        HandleBlockFx(reader);
                        break;
                    case SoulbindingOp.State:
                        HandleState(reader, whoAmI);
                        break;
                }
            } catch (EndOfStreamException) {
            } catch (IOException) {
            }
        }

        private static void HandleGain(BinaryReader reader, int whoAmI) {
            //先读完全部载荷再校验早退
            int player = reader.ReadByte();
            int count = reader.ReadByte();
            Vector2 from = reader.ReadVector2();

            if (Main.netMode == NetmodeID.Server) {
                if (player != whoAmI) {
                    CWRMod.Instance.Logger.Info($"SoulbindingArm gain spoof dropped: claim={player} actual={whoAmI}");
                    return;
                }
                ApplyCount(player, count);
                ModPacket relay = NewPacket(SoulbindingOp.Gain);
                relay.Write((byte)player);
                relay.Write((byte)count);
                relay.WriteVector2(from);
                relay.Send(ignoreClient: whoAmI);
                return;
            }

            if (ApplyCount(player, count)
                && Main.player[player].TryGetModPlayer(out SoulbindingArmPlayer mp)) {
                SoulbindingArmRender.GainFx(mp, from);
            }
        }

        private static void HandleBlockRequest(BinaryReader reader, int whoAmI) {
            //先读完全部载荷（TryRead 失败也已消费定长字节，流对齐无虞）
            bool identityOk = NetworkProjectileIdentity.TryRead(reader, out NetworkProjectileIdentity identity);
            int count = reader.ReadByte();
            Vector2 pos = reader.ReadVector2();

            if (Main.netMode != NetmodeID.Server) {
                return;
            }
            if (whoAmI < 0 || whoAmI >= Main.maxPlayers || Main.player[whoAmI]?.active != true) {
                return;
            }
            //限频：拥有者扫描每 tick 至多一发，超频视作伪造直接丢弃（镜像靠慢对账自愈）
            if (lastBlockFrame.TryGetValue(whoAmI, out uint last) && last == Main.GameUpdateCount) {
                CWRMod.Instance.Logger.Info($"SoulbindingArm block rate-limited: player={whoAmI}");
                return;
            }
            lastBlockFrame[whoAmI] = (uint)Main.GameUpdateCount;

            //权威灭杀：身份解析失败（同帧自然死亡的竞态）不视作错误，计数照常转播
            if (identityOk && identity.TryResolve(out Projectile proj)
                && proj.hostile && !proj.friendly
                && Main.player[whoAmI].WithinRange(proj.Center, BlockValidateRange)) {
                //敌方弹幕 owner=255 与服务端 myPlayer 一致，Kill 自带 KillProjectile 广播
                proj.Kill();
            }

            ApplyCount(whoAmI, count);
            ModPacket relay = NewPacket(SoulbindingOp.BlockFx);
            relay.Write((byte)whoAmI);
            relay.Write((byte)count);
            relay.WriteVector2(pos);
            relay.Send(ignoreClient: whoAmI);
        }

        private static void HandleBlockFx(BinaryReader reader) {
            int player = reader.ReadByte();
            int count = reader.ReadByte();
            Vector2 pos = reader.ReadVector2();

            if (Main.netMode != NetmodeID.MultiplayerClient) {
                return;
            }
            ApplyCount(player, count);
            SoulbindingArmRender.BlockPopFx(pos);
        }

        private static void HandleState(BinaryReader reader, int whoAmI) {
            int player = reader.ReadByte();
            int count = reader.ReadByte();

            if (Main.netMode == NetmodeID.Server) {
                if (player != whoAmI) {
                    CWRMod.Instance.Logger.Info($"SoulbindingArm state spoof dropped: claim={player} actual={whoAmI}");
                    return;
                }
                ApplyCount(player, count);
                ModPacket relay = NewPacket(SoulbindingOp.State);
                relay.Write((byte)player);
                relay.Write((byte)count);
                relay.Send(ignoreClient: whoAmI);
                return;
            }
            ApplyCount(player, count);
        }

        /// <summary>写入指定玩家的魂数镜像，返回是否成功落位</summary>
        private static bool ApplyCount(int player, int count) {
            if (player < 0 || player >= Main.maxPlayers) {
                return false;
            }
            Player plr = Main.player[player];
            if (plr?.active != true || !plr.TryGetModPlayer(out SoulbindingArmPlayer mp)) {
                return false;
            }
            mp.SoulCount = Math.Clamp(count, 0, SoulbindingArmPlayer.MaxSouls);
            return true;
        }
        #endregion
    }
}

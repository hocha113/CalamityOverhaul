using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaTalismans
{
    internal enum KikasaTalismanNetOp : byte
    {
        //0..4 曾是符箧快照与物品级挂符请求/回执（符位表 2026-08 迁玩家侧后退役：
        //服务器不再仲裁挂符，符箧成 owner 本机数据）。tML 强制两端模组同版本，
        //旧包不会跨版本到达，编号可安全复用
        RopeSnapshot = 0,
    }

    /// <summary>
    /// 祈雨绳符位表快照信道，类本身即信道：owner 本机写入
    /// <see cref="KikasaTalismanPlayer.Talismans"/> 后推快照，服务器登记并转播给旁观端
    /// （旁观端的伞节拍/挂钩演出照常解析），SyncPlayer 把存量补给晚入场者。
    /// 配置归玩家（一人一套），挂/摘/换不再走请求-回执
    /// </summary>
    internal class KikasaTalismanNet : CWRNetChannel
    {
        private const ulong SnapshotWindowTicks = 60;
        private const int MaxSnapshotsPerWindow = 12;

        private struct SnapshotWindow
        {
            internal ulong StartedAt;
            internal int Count;
        }

        private static readonly Dictionary<int, SnapshotWindow> snapshotWindows = [];

        public override void Receive(BinaryReader reader, int whoAmI) {
            KikasaTalismanNetOp op = (KikasaTalismanNetOp)reader.ReadByte();
            if (op == KikasaTalismanNetOp.RopeSnapshot) {
                ReceiveRopeSnapshot(reader, whoAmI);
            }
        }

        /// <summary>
        /// 本机符位表快照推给服务器；服务端语境（SyncPlayer 补晚入场者/登记后转播）
        /// 按 <paramref name="toWho"/>/<paramref name="fromWho"/> 转发存量
        /// </summary>
        internal static void SendRopeSnapshot(Player player, int toWho = -1, int fromWho = -1) {
            if (Main.netMode == NetmodeID.SinglePlayer || player == null
                || !player.TryGetModPlayer(out KikasaTalismanPlayer ktp)) {
                return;
            }
            if (Main.netMode == NetmodeID.MultiplayerClient && player.whoAmI != Main.myPlayer) {
                return;
            }
            ModPacket packet = CWRNetWork.GetPacket<KikasaTalismanNet>();
            packet.Write((byte)KikasaTalismanNetOp.RopeSnapshot);
            packet.Write((byte)player.whoAmI);
            ktp.Talismans.NetSend(packet);
            if (Main.netMode == NetmodeID.MultiplayerClient) {
                packet.Send();
            }
            else {
                packet.Send(toWho, fromWho);
            }
        }

        private static void ReceiveRopeSnapshot(BinaryReader reader, int whoAmI) {
            //先把负载读干净再做守卫：提前 return 会在 HandlePacket 留下未读字节
            int playerIndex = reader.ReadByte();
            KikasaTalismanStore incoming = new();
            incoming.NetReceive(reader);
            if (playerIndex >= Main.maxPlayers) {
                return;
            }

            if (Main.netMode == NetmodeID.Server) {
                //快照只认本人；限频防逐帧刷包被放大成 N-1 转播（正常编辑手速远够不着上限）
                if (playerIndex != whoAmI || !AllowSnapshot(whoAmI)) {
                    CWRMod.Instance.Logger.Info(
                        $"[KikasaTalismanNet] rope snapshot dropped from player {whoAmI}, claimed index {playerIndex}");
                    return;
                }
                //符位表是存档状态，进世界那帧玩家还没落地，不能按存活筛
                Player sender = ResolveSender(whoAmI, requireAlive: false);
                if (sender == null || !sender.TryGetModPlayer(out KikasaTalismanPlayer ktp)) {
                    return;
                }
                ktp.Talismans.CopyFrom(incoming);
                //登记后转播旁观端；不回发送者，owner 本机即真相
                SendRopeSnapshot(sender, -1, whoAmI);
                return;
            }
            if (Main.netMode != NetmodeID.MultiplayerClient) {
                return;
            }
            //本机真相不吃回显，旁观玩家照抄
            if (playerIndex == Main.myPlayer) {
                return;
            }
            Player remote = Main.player[playerIndex];
            if (remote?.active == true
                && remote.TryGetModPlayer(out KikasaTalismanPlayer rtp)) {
                rtp.Talismans.CopyFrom(incoming);
            }
        }

        private static bool AllowSnapshot(int whoAmI) {
            ulong now = Main.GameUpdateCount;
            if (!snapshotWindows.TryGetValue(whoAmI, out SnapshotWindow window)
                || now - window.StartedAt >= SnapshotWindowTicks) {
                snapshotWindows[whoAmI] = new SnapshotWindow {
                    StartedAt = now,
                    Count = 1,
                };
                return true;
            }
            if (window.Count >= MaxSnapshotsPerWindow) {
                return false;
            }
            window.Count++;
            snapshotWindows[whoAmI] = window;
            return true;
        }

        private static Player ResolveSender(int whoAmI, bool requireAlive = true) {
            if (whoAmI < 0 || whoAmI >= Main.maxPlayers) {
                return null;
            }
            Player player = Main.player[whoAmI];
            return player?.active == true && (!requireAlive || !player.dead)
                ? player
                : null;
        }
    }
}

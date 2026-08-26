using CalamityOverhaul.Content.Scenarios.Dungeonworld.NPCs;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.BossRooms
{
    /// <summary>
    /// 禁室房态网络通道。tile 本身随区块自然同步，这里只同步注册表与相位，
    /// 供客户端本地演出（氛围层、封门能量栅、相位音效）取用：<br/>
    /// 客户端进世界发 FullSyncRequest，服务器把全表点发回去；相位变更时服务器全播增量。<br/>
    /// 门禁关闭时两端都不会发包（看守短路、注册表恒空）；收包侧仍按
    /// 「字节读净再校验」纪律处理，防御异常端点。
    /// </summary>
    internal class GaolRoomNet : CWRNetChannel
    {
        private enum Op : byte
        {
            FullSyncRequest = 0,
            FullSync = 1,
            PhaseUpdate = 2,
        }

        //==================== 发包 ====================

        /// <summary>客户端进世界（含断线重连）请求全量房态</summary>
        internal static void RequestFullSync() {
            if (!DeepGaolWraithGate.Enabled || Main.netMode != NetmodeID.MultiplayerClient) {
                return;
            }
            ModPacket packet = CWRNetWork.GetPacket<GaolRoomNet>();
            packet.Write((byte)Op.FullSyncRequest);
            packet.Send();
        }

        /// <summary>服务器点发全量房态（toWho=-1 全播，用于世界生成后兜底）</summary>
        internal static void SendFullSync(int toWho) {
            if (!DeepGaolWraithGate.Enabled || Main.netMode != NetmodeID.Server) {
                return;
            }
            List<GaolBossRoomWatcher.RoomState> rooms = GaolBossRoomWatcher.Rooms;
            ModPacket packet = CWRNetWork.GetPacket<GaolRoomNet>();
            packet.Write((byte)Op.FullSync);
            packet.Write((byte)rooms.Count);
            foreach (GaolBossRoomWatcher.RoomState room in rooms) {
                packet.Write(room.Origin.X);
                packet.Write(room.Origin.Y);
                packet.Write((byte)room.Phase);
            }
            packet.Send(toWho);
        }

        /// <summary>服务器全播单房相位变更（看守 SetPhase 唯一出口调用）</summary>
        internal static void BroadcastPhase(Point origin, GaolRoomPhase phase) {
            if (!DeepGaolWraithGate.Enabled || Main.netMode != NetmodeID.Server) {
                return;
            }
            ModPacket packet = CWRNetWork.GetPacket<GaolRoomNet>();
            packet.Write((byte)Op.PhaseUpdate);
            packet.Write(origin.X);
            packet.Write(origin.Y);
            packet.Write((byte)phase);
            packet.Send();
        }

        //==================== 收包（字节读净再校验）====================

        public override void Receive(BinaryReader reader, int whoAmI) {
            Op op = (Op)reader.ReadByte();
            switch (op) {
                case Op.FullSyncRequest:
                    //载荷为空；仅服务器响应，向发起端点发全表
                    if (Main.netMode == NetmodeID.Server && DeepGaolWraithGate.Enabled) {
                        SendFullSync(whoAmI);
                    }
                    break;

                case Op.FullSync: {
                    int count = reader.ReadByte();
                    var incoming = new List<(Point origin, GaolRoomPhase phase)>(count);
                    for (int i = 0; i < count; i++) {
                        int x = reader.ReadInt32();
                        int y = reader.ReadInt32();
                        byte phase = reader.ReadByte();
                        incoming.Add((new Point(x, y), (GaolRoomPhase)phase));
                    }
                    if (Main.netMode != NetmodeID.MultiplayerClient || !DeepGaolWraithGate.Enabled) {
                        return;
                    }
                    GaolBossRoomWatcher.Rooms.Clear();
                    foreach ((Point origin, GaolRoomPhase phase) in incoming) {
                        if (!WorldGen.InWorld(origin.X, origin.Y, 5)) {
                            continue;
                        }
                        GaolBossRoomWatcher.Rooms.Add(new GaolBossRoomWatcher.RoomState {
                            Origin = origin,
                            Phase = SanitizePhase(phase),
                        });
                    }
                    break;
                }

                case Op.PhaseUpdate: {
                    int x = reader.ReadInt32();
                    int y = reader.ReadInt32();
                    byte rawPhase = reader.ReadByte();
                    if (Main.netMode != NetmodeID.MultiplayerClient || !DeepGaolWraithGate.Enabled
                        || !WorldGen.InWorld(x, y, 5)) {
                        return;
                    }
                    Point origin = new(x, y);
                    GaolRoomPhase phase = SanitizePhase((GaolRoomPhase)rawPhase);
                    GaolBossRoomWatcher.RoomState room = null;
                    foreach (GaolBossRoomWatcher.RoomState candidate in GaolBossRoomWatcher.Rooms) {
                        if (candidate.Origin == origin) {
                            room = candidate;
                            break;
                        }
                    }
                    if (room == null) {
                        //增量先于全量到达（世界刚生成）：直接补登，后续全量会覆盖
                        room = new GaolBossRoomWatcher.RoomState { Origin = origin };
                        GaolBossRoomWatcher.Rooms.Add(room);
                    }
                    if (room.Phase != phase) {
                        room.Phase = phase;
                        GaolBossRoomWatcher.PlayPhaseCue(origin, phase);
                    }
                    break;
                }
            }
        }

        private static GaolRoomPhase SanitizePhase(GaolRoomPhase phase)
            => phase > GaolRoomPhase.Cleared ? GaolRoomPhase.Dormant : phase;
    }

    /// <summary>进世界（含断线重连）时客户端向服务器要一次全量房态</summary>
    internal class GaolRoomNetPlayer : ModPlayer
    {
        public override bool IsLoadingEnabled(Mod mod) => DeepGaolWraithGate.Enabled;

        public override void OnEnterWorld() => GaolRoomNet.RequestFullSync();
    }
}

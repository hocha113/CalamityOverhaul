using CalamityOverhaul.Content.TimeFreezes;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.DomainSkills.Restarts
{
    /// <summary>
    /// 比目鱼大范围重启三段包：Request（客户端→服务器，携带按层数推算的作用半径，
    /// 档位门由客户端预检，服务器没有领域状态是既定契约，半径服务器只做钳制）；
    /// Apply（服务器→全体，owner+resetId+seed+range+NPC 身份组+玩家组，各端时间轴自此起跑）；
    /// Cancel（服务器→全体，施术者掉线的收场令）。
    /// 所有字段先读满，校验只做丢弃；计数一律 byte，maxNPCs=200、maxPlayers=255，天然有界
    /// </summary>
    internal class HalibutResetNet : CWRNetChannel
    {
        private const byte OpRequest = 0;
        private const byte OpApply = 1;
        private const byte OpCancel = 2;

        internal static void SendRequest(float range) {
            if (Main.netMode != NetmodeID.MultiplayerClient) {
                return;
            }
            ModPacket packet = CWRNetWork.GetPacket<HalibutResetNet>();
            packet.Write(OpRequest);
            packet.Write(range);
            packet.Send();
        }

        internal static void SendApply(HalibutReset.ResetShow show) {
            if (Main.netMode != NetmodeID.Server || show == null) {
                return;
            }
            ModPacket packet = CWRNetWork.GetPacket<HalibutResetNet>();
            packet.Write(OpApply);
            packet.Write((byte)show.OwnerWho);
            packet.Write(show.ResetId);
            packet.Write(show.Seed);
            packet.Write(show.Range);
            packet.Write((byte)show.Npcs.Count);
            for (int i = 0; i < show.Npcs.Count; i++) {
                show.Npcs[i].Write(packet);
            }
            packet.Write((byte)show.Players.Count);
            for (int i = 0; i < show.Players.Count; i++) {
                packet.Write((byte)show.Players[i]);
            }
            packet.Send();
        }

        internal static void SendCancel(int resetId) {
            if (Main.netMode != NetmodeID.Server) {
                return;
            }
            ModPacket packet = CWRNetWork.GetPacket<HalibutResetNet>();
            packet.Write(OpCancel);
            packet.Write(resetId);
            packet.Send();
        }

        public override void Receive(BinaryReader reader, int whoAmI) {
            byte op = reader.ReadByte();
            switch (op) {
                case OpRequest: {
                    float range = reader.ReadSingle();
                    if (Main.netMode == NetmodeID.Server) {
                        //来源以连接为准
                        HalibutReset.HandleRequest(whoAmI, range);
                    }
                    break;
                }
                case OpApply: {
                    //按声明数读满，无效身份读完再丢
                    int owner = reader.ReadByte();
                    int resetId = reader.ReadInt32();
                    float seed = reader.ReadSingle();
                    float range = reader.ReadSingle();
                    int npcCount = reader.ReadByte();
                    List<NetworkNPCIdentity> npcs = [];
                    bool corrupt = false;
                    for (int i = 0; i < npcCount; i++) {
                        if (NetworkNPCIdentity.TryRead(reader, out NetworkNPCIdentity id)) {
                            npcs.Add(id);
                        }
                        else {
                            corrupt = true;
                        }
                    }
                    int playerCount = reader.ReadByte();
                    List<int> players = [];
                    for (int i = 0; i < playerCount; i++) {
                        int who = reader.ReadByte();
                        if (who < Main.maxPlayers) {
                            players.Add(who);
                        }
                    }
                    if (Main.netMode != NetmodeID.MultiplayerClient
                        || owner >= Main.maxPlayers || corrupt
                        || !float.IsFinite(seed) || !float.IsFinite(range)) {
                        break;
                    }
                    HalibutReset.StartShow(owner, resetId, seed, range, npcs, players);
                    break;
                }
                case OpCancel: {
                    int resetId = reader.ReadInt32();
                    if (Main.netMode == NetmodeID.MultiplayerClient) {
                        HalibutReset.HandleCancel(resetId);
                    }
                    break;
                }
            }
        }
    }
}

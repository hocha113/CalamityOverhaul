using CalamityOverhaul.Content.TimeFreezes;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDrowns
{
    /// <summary>
    /// 沉溺六段包：Request（客户端→服务器，index+type+generation，
    /// generation 允许为 0：客户端可能铸不出章，服务器按 index+type 回退再盖自己的；
    /// 服务器解析后按门槛分流沉溺或鞭笞）；
    /// Apply（服务器→全体，owner+drownId+seed+身份组，各端演出时间轴自此起跑）；
    /// Cancel（服务器→全体，目标提前没了的谢幕令）；
    /// Complete（服务器→全体，权威完成帧的沉湖记忆通报，仅所有者本机入账）；
    /// ScourgeApply（服务器→全体，鞭笞/自动鞭击起演令，目标死亡由演出自察无需取消令）；
    /// AmbientRequest（客户端→服务器，自动鞭击索敌上行，载荷同 Request）。
    /// 链式 handler 共用一条流：所有字段先读满，校验只做丢弃。
    /// </summary>
    internal static class KikasaDrownNet
    {
        private const byte OpRequest = 0;
        private const byte OpApply = 1;
        private const byte OpCancel = 2;
        private const byte OpComplete = 3;
        private const byte OpScourgeApply = 4;
        private const byte OpAmbientRequest = 5;

        internal static void SendRequest(int npcIndex, int npcType, ulong generation) {
            if (Main.netMode != NetmodeID.MultiplayerClient) {
                return;
            }
            ModPacket packet = CWRMod.Instance.GetPacket();
            packet.Write((byte)CWRMessageType.KikasaDrown);
            packet.Write(OpRequest);
            packet.Write((ushort)npcIndex);
            packet.Write(npcType);
            packet.Write(generation);
            packet.Send();
        }

        internal static void SendApply(KikasaDrown.DrownActivation activation) {
            if (Main.netMode != NetmodeID.Server || activation == null) {
                return;
            }
            ModPacket packet = CWRMod.Instance.GetPacket();
            packet.Write((byte)CWRMessageType.KikasaDrown);
            packet.Write(OpApply);
            packet.Write((byte)activation.OwnerWho);
            packet.Write(activation.DrownId);
            packet.Write(activation.Seed);
            packet.Write((byte)activation.Targets.Count);
            for (int i = 0; i < activation.Targets.Count; i++) {
                activation.Targets[i].Identity.Write(packet);
            }
            packet.Send();
        }

        internal static void SendCancel(int drownId) {
            if (Main.netMode != NetmodeID.Server) {
                return;
            }
            ModPacket packet = CWRMod.Instance.GetPacket();
            packet.Write((byte)CWRMessageType.KikasaDrown);
            packet.Write(OpCancel);
            packet.Write(drownId);
            packet.Send();
        }

        internal static void SendComplete(int ownerWho, int npcType) {
            if (Main.netMode != NetmodeID.Server) {
                return;
            }
            ModPacket packet = CWRMod.Instance.GetPacket();
            packet.Write((byte)CWRMessageType.KikasaDrown);
            packet.Write(OpComplete);
            packet.Write((byte)ownerWho);
            packet.Write(npcType);
            packet.Send();
        }

        internal static void SendScourgeApply(KikasaScourge.ScourgeActivation activation) {
            if (Main.netMode != NetmodeID.Server || activation == null) {
                return;
            }
            ModPacket packet = CWRMod.Instance.GetPacket();
            packet.Write((byte)CWRMessageType.KikasaDrown);
            packet.Write(OpScourgeApply);
            packet.Write((byte)activation.OwnerWho);
            packet.Write(activation.ScourgeId);
            packet.Write(activation.Seed);
            packet.Write(activation.Kind);
            activation.Target.Write(packet);
            packet.Send();
        }

        internal static void SendAmbientRequest(int npcIndex, int npcType, ulong generation) {
            if (Main.netMode != NetmodeID.MultiplayerClient) {
                return;
            }
            ModPacket packet = CWRMod.Instance.GetPacket();
            packet.Write((byte)CWRMessageType.KikasaDrown);
            packet.Write(OpAmbientRequest);
            packet.Write((ushort)npcIndex);
            packet.Write(npcType);
            packet.Write(generation);
            packet.Send();
        }

        public static void NetHandle(CWRMessageType type, BinaryReader reader, int whoAmI) {
            if (type != CWRMessageType.KikasaDrown) {
                return;
            }
            byte op = reader.ReadByte();
            switch (op) {
                case OpRequest: {
                    int npcIndex = reader.ReadUInt16();
                    int npcType = reader.ReadInt32();
                    ulong generation = reader.ReadUInt64();
                    if (Main.netMode == NetmodeID.Server) {
                        //来源以连接为准
                        KikasaDrown.HandleRequest(whoAmI, npcIndex, npcType, generation);
                    }
                    break;
                }
                case OpApply: {
                    int owner = reader.ReadByte();
                    int drownId = reader.ReadInt32();
                    float seed = reader.ReadSingle();
                    int count = reader.ReadByte();
                    //按声明数读满，无效身份读完再丢
                    List<NetworkNPCIdentity> identities = [];
                    bool corrupt = false;
                    for (int i = 0; i < count; i++) {
                        if (NetworkNPCIdentity.TryRead(reader, out NetworkNPCIdentity id)) {
                            identities.Add(id);
                        }
                        else {
                            corrupt = true;
                        }
                    }
                    if (Main.netMode != NetmodeID.MultiplayerClient
                        || owner < 0 || owner >= Main.maxPlayers
                        || count <= 0 || identities.Count == 0 || corrupt
                        || !float.IsFinite(seed)) {
                        break;
                    }
                    NetworkNPCIdentity primary = identities[0];
                    identities.RemoveAt(0);
                    KikasaDrownFX.StartShow(owner, drownId, seed, primary, identities);
                    break;
                }
                case OpCancel: {
                    int drownId = reader.ReadInt32();
                    if (Main.netMode == NetmodeID.MultiplayerClient) {
                        KikasaDrownFX.CancelShow(drownId);
                    }
                    break;
                }
                case OpComplete: {
                    //先读满再校验；记忆只归所有者本机
                    int owner = reader.ReadByte();
                    int npcType = reader.ReadInt32();
                    if (Main.netMode == NetmodeID.MultiplayerClient
                        && owner == Main.myPlayer
                        && Main.LocalPlayer?.active == true) {
                        Main.LocalPlayer
                            .GetModPlayer<KikasaServants.KikasaServantPlayer>()
                            .RecordDrowned(npcType);
                    }
                    break;
                }
                case OpScourgeApply: {
                    int owner = reader.ReadByte();
                    int scourgeId = reader.ReadInt32();
                    float seed = reader.ReadSingle();
                    byte kind = reader.ReadByte();
                    bool valid = NetworkNPCIdentity.TryRead(reader, out NetworkNPCIdentity identity);
                    if (Main.netMode != NetmodeID.MultiplayerClient
                        || owner < 0 || owner >= Main.maxPlayers
                        || !valid || !float.IsFinite(seed)
                        || kind > KikasaScourge.KindAmbient) {
                        break;
                    }
                    KikasaScourgeFX.StartShow(owner, scourgeId, seed, kind, identity);
                    break;
                }
                case OpAmbientRequest: {
                    int npcIndex = reader.ReadUInt16();
                    int npcType = reader.ReadInt32();
                    ulong generation = reader.ReadUInt64();
                    if (Main.netMode == NetmodeID.Server) {
                        //来源以连接为准
                        KikasaScourge.HandleAmbientRequest(whoAmI, npcIndex, npcType, generation);
                    }
                    break;
                }
            }
        }
    }
}

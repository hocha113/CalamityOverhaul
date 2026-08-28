using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDrowns
{
    /// <summary>
    /// 沉玩家三段包：Request（客户端→服务器，victim+lakeY，水线是施术者报的骰，
    /// 服务器只做幅度钳制，来源以连接为准）；
    /// Apply（服务器→全体，owner+victim+bindId+seed+lakeY+elapsed，收端幂等、
    /// 计时只快进不回拨，服务器定期重播同一包自愈丢包与中途加入）；
    /// Cancel（服务器→全体，提前松手令：施术者死亡/受害者离场）。
    /// 所有字段先读满，校验只做丢弃
    /// </summary>
    internal class KikasaPlayerDrownNet : CWRNetChannel
    {
        private const byte OpRequest = 0;
        private const byte OpApply = 1;
        private const byte OpCancel = 2;

        internal static void SendRequest(int victimWho, float lakeY) {
            if (Main.netMode != NetmodeID.MultiplayerClient) {
                return;
            }
            ModPacket packet = CWRNetWork.GetPacket<KikasaPlayerDrownNet>();
            packet.Write(OpRequest);
            packet.Write((byte)victimWho);
            packet.Write(lakeY);
            packet.Send();
        }

        internal static void SendApply(KikasaPlayerDrown.BindActivation activation) {
            if (Main.netMode != NetmodeID.Server || activation == null) {
                return;
            }
            ModPacket packet = CWRNetWork.GetPacket<KikasaPlayerDrownNet>();
            packet.Write(OpApply);
            packet.Write((byte)activation.OwnerWho);
            packet.Write((byte)activation.VictimWho);
            packet.Write(activation.BindId);
            packet.Write(activation.Seed);
            packet.Write(activation.LakeY);
            packet.Write((ushort)System.Math.Clamp(activation.Timer, 0, ushort.MaxValue));
            packet.Send();
        }

        internal static void SendCancel(int bindId) {
            if (Main.netMode != NetmodeID.Server) {
                return;
            }
            ModPacket packet = CWRNetWork.GetPacket<KikasaPlayerDrownNet>();
            packet.Write(OpCancel);
            packet.Write(bindId);
            packet.Send();
        }

        public override void Receive(BinaryReader reader, int whoAmI) {
            byte op = reader.ReadByte();
            switch (op) {
                case OpRequest: {
                    int victimWho = reader.ReadByte();
                    float lakeY = reader.ReadSingle();
                    if (Main.netMode == NetmodeID.Server) {
                        //来源以连接为准，不信包里自报的施术者
                        KikasaPlayerDrown.HandleRequest(whoAmI, victimWho, lakeY);
                    }
                    break;
                }
                case OpApply: {
                    //定长负载先读满，校验只做丢弃
                    int ownerWho = reader.ReadByte();
                    int victimWho = reader.ReadByte();
                    int bindId = reader.ReadInt32();
                    float seed = reader.ReadSingle();
                    float lakeY = reader.ReadSingle();
                    int elapsed = reader.ReadUInt16();
                    if (Main.netMode != NetmodeID.MultiplayerClient
                        || ownerWho < 0 || ownerWho >= Main.maxPlayers
                        || victimWho < 0 || victimWho >= Main.maxPlayers
                        || ownerWho == victimWho
                        || !float.IsFinite(seed) || !float.IsFinite(lakeY)) {
                        break;
                    }
                    KikasaPlayerDrown.ApplyFromNet(ownerWho, victimWho, bindId, seed, lakeY, elapsed);
                    break;
                }
                case OpCancel: {
                    int bindId = reader.ReadInt32();
                    if (Main.netMode == NetmodeID.MultiplayerClient) {
                        KikasaPlayerDrown.CancelFromNet(bindId);
                    }
                    break;
                }
            }
        }
    }
}

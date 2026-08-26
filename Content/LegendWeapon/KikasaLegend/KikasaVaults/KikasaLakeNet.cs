using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaVaults
{
    /// <summary>
    /// 湖藏演出转播。纯表现包：只播"谁、沉还是浮、什么物品"，
    /// 幽灵的坐标与水位由各端按所有者当前形态自算（领域形态由 KikasaDomainNet 兜着）；
    /// 湖藏数据是所有者本机私产，不经过这条通道。
    /// </summary>
    internal class KikasaLakeNet : CWRNetChannel
    {
        internal const byte KindSink = 0;
        internal const byte KindRaise = 1;

        /// <summary>本机所有者演出成立后转播一份，让同场的人看见沉浮</summary>
        internal static void SendFX(Player owner, byte kind, int itemType) {
            if (Main.netMode != NetmodeID.MultiplayerClient || owner == null
                || owner.whoAmI != Main.myPlayer) {
                return;
            }
            ModPacket packet = CWRNetWork.GetPacket<KikasaLakeNet>();
            packet.Write((byte)owner.whoAmI);
            packet.Write(kind);
            packet.Write(itemType);
            packet.Send();
        }

        public override void Receive(BinaryReader reader, int whoAmI) {
            //定长负载先读满，校验只做丢弃
            int declaredOwner = reader.ReadByte();
            byte kind = reader.ReadByte();
            int itemType = reader.ReadInt32();

            if (Main.netMode == NetmodeID.Server) {
                //来源以连接为准，不信包里自报的槽位；原样转播给除发送者外的所有人
                ModPacket packet = CWRNetWork.GetPacket<KikasaLakeNet>();
                packet.Write((byte)whoAmI);
                packet.Write(kind);
                packet.Write(itemType);
                packet.Send(-1, whoAmI);
                return;
            }
            if (Main.netMode != NetmodeID.MultiplayerClient
                || declaredOwner < 0 || declaredOwner >= Main.maxPlayers
                || declaredOwner == Main.myPlayer) {
                return;
            }
            if (kind > KindRaise || itemType <= ItemID.None || itemType >= ItemLoader.ItemCount) {
                return;
            }
            Player owner = Main.player[declaredOwner];
            if (owner?.active != true) {
                return;
            }
            if (kind == KindSink) {
                KikasaLakeFX.SpawnSinkCore(owner, itemType);
            }
            else {
                //远端无在途实体，幽灵只演不交付
                KikasaLakeFX.SpawnRaiseCore(owner, itemType, null);
            }
        }
    }
}

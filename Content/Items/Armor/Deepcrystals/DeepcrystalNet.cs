using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Armor.Deepcrystals
{
    /// <summary>
    /// 聚泡层数转播:owner 端层数变化时推给同场其他人,旁观端据此画环绕气泡与引爆/碎泡演出
    /// (施法者本地演出旁观不可见的老坑)。引爆弹幕本身走 NewProjectile 原生同步,不经此信道
    /// </summary>
    internal class DeepcrystalNet : CWRNetChannel
    {
        internal const byte FlagDetonate = 1;
        internal const byte FlagShatter = 2;

        /// <summary>把本机层数推给全场;单人模式静默</summary>
        internal static void Send(Player player, byte charge, byte flags) {
            if (Main.netMode != NetmodeID.MultiplayerClient || player.whoAmI != Main.myPlayer) {
                return;
            }
            ModPacket packet = CWRNetWork.GetPacket<DeepcrystalNet>();
            packet.Write((byte)player.whoAmI);
            packet.Write(charge);
            packet.Write(flags);
            packet.Send();
        }

        public override void Receive(BinaryReader reader, int whoAmI) {
            int declaredOwner = reader.ReadByte();
            byte charge = reader.ReadByte();
            byte flags = reader.ReadByte();

            if (Main.netMode == NetmodeID.Server) {
                //来源以连接为准,不信包里自报的槽位;原样转播给除发送者外的所有人
                ModPacket packet = CWRNetWork.GetPacket<DeepcrystalNet>();
                packet.Write((byte)whoAmI);
                packet.Write(charge);
                packet.Write(flags);
                packet.Send(-1, whoAmI);
                return;
            }
            if (declaredOwner < 0 || declaredOwner >= Main.maxPlayers || declaredOwner == Main.myPlayer) {
                return;
            }
            Player owner = Main.player[declaredOwner];
            if (owner?.active != true || !owner.TryGetModPlayer(out DeepcrystalPlayer dcp)) {
                return;
            }
            dcp.ApplyNetCharge(charge, flags);
        }
    }
}

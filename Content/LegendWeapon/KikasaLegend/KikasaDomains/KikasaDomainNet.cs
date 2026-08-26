using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains
{
    /// <summary>
    /// 血湖领域的表现层同步。领域是施术者本机的状态机，这里只把它的形态转播给同场的人，
    /// 让队友能看见别人开的域；服务端不持有也不推进领域，判定不经过这条通道。
    /// </summary>
    internal class KikasaDomainNet : CWRNetChannel
    {
        /// <summary>稳态下的重播间隔。中途加入、丢包、漂移都靠它自愈</summary>
        internal const int ResyncInterval = 120;

        /// <summary>把本机领域形态推给同世界的其他人</summary>
        internal static void SendSnapshot(Player player) {
            if (Main.netMode != NetmodeID.MultiplayerClient || player == null
                || player.whoAmI != Main.myPlayer
                || !player.TryGetModPlayer(out KikasaDomainPlayer domain)) {
                return;
            }
            using MemoryStream stream = new();
            using BinaryWriter stateWriter = new(stream);
            domain.WriteNetworkState(stateWriter);
            byte[] state = stream.ToArray();

            ModPacket packet = CWRNetWork.GetPacket<KikasaDomainNet>();
            packet.Write((byte)player.whoAmI);
            //自带长度：读端按声明长度读满，坏包只废自己不扩散
            packet.Write((byte)state.Length);
            packet.Write(state);
            packet.Send();
        }

        public override void Receive(BinaryReader reader, int whoAmI) {
            //按写入端声明的长度读满再校验
            int declaredOwner = reader.ReadByte();
            int declaredLength = reader.ReadByte();
            byte[] state = reader.ReadBytes(declaredLength);
            //读不满说明来源流已经坏了，转播出去只会把错位扩散给全场
            if (declaredLength == 0 || state.Length != declaredLength) {
                return;
            }

            if (Main.netMode == NetmodeID.Server) {
                //来源以连接为准，不信包里自报的槽位；原样转播给除发送者外的所有人
                ModPacket packet = CWRNetWork.GetPacket<KikasaDomainNet>();
                packet.Write((byte)whoAmI);
                packet.Write((byte)state.Length);
                packet.Write(state);
                packet.Send(-1, whoAmI);
                return;
            }
            if (Main.netMode != NetmodeID.MultiplayerClient
                || declaredOwner < 0 || declaredOwner >= Main.maxPlayers
                || declaredOwner == Main.myPlayer) {
                return;
            }

            Player owner = Main.player[declaredOwner];
            if (owner?.active != true
                || !owner.TryGetModPlayer(out KikasaDomainPlayer domain)) {
                return;
            }
            //子流独立，读坏了也带不动主封包的对齐
            using MemoryStream stream = new(state);
            using BinaryReader stateReader = new(stream);
            try {
                domain.ReadNetworkState(stateReader);
            } catch (EndOfStreamException) {
            }
        }
    }
}

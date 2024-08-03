using CalamityOverhaul.Content.TileEntitys.Core;
using CalamityOverhaul.Content.Tiles;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.TileEntitys
{
    internal class TETram : BaseCWRTE
    {
        public override int TileType => ModContent.TileType<TransmutationOfMatter>();

        public override int Width => TransmutationOfMatter.Width;

        public override int Height => TransmutationOfMatter.Height;

        public Vector2 Center => Position.ToWorldCoordinates(8 * TransmutationOfMatter.Width, 8 * TransmutationOfMatter.Height);

        private const int maxleng = 300;

        public override void OnKill() {
            foreach (var p in Main.player) {
                if (!p.active) {
                    continue;
                }
                if (p.CWR().TETramContrType == Type) {
                    p.CWR().SupertableUIStartBool = false;
                }
            }
        }

        public override void AI() {
            Player player = Main.LocalPlayer;
            if (!player.active || Main.myPlayer != player.whoAmI) {
                return;
            }
            CWRPlayer modPlayer = player.CWR();
            if (!modPlayer.SupertableUIStartBool || CWRUtils.isServer) {
                return;
            }
            float leng = Center.Distance(player.Center);
            if ((leng >= maxleng || player.dead) && modPlayer.TETramContrType == ID) {
                modPlayer.SupertableUIStartBool = false;
                modPlayer.TETramContrType = 0;
                SoundEngine.PlaySound(SoundID.MenuClose with { Pitch = -0.2f });
            }
        }
    }
}

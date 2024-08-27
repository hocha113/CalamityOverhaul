using CalamityOverhaul.Content.TileModules.Core;
using CalamityOverhaul.Content.Tiles;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.TileModules
{
    internal class TramModule : BaseTileModule
    {
        public override int TargetTileID => ModContent.TileType<TransmutationOfMatter>();
        private const int maxleng = 300;
        public override void Update() {
            Player player = Main.LocalPlayer;
            if (!player.active || Main.myPlayer != player.whoAmI) {
                return;
            }

            CWRPlayer modPlayer = player.CWR();
            if (!modPlayer.SupertableUIStartBool || CWRUtils.isServer) {
                return;
            }

            float leng = PosInWorld.Distance(player.Center);
            if ((leng >= maxleng || player.dead) && modPlayer.TETramContrType == WhoAmI) {
                modPlayer.SupertableUIStartBool = false;
                modPlayer.TETramContrType = -1;
                SoundEngine.PlaySound(SoundID.MenuClose with { Pitch = -0.2f });
            }
        }

        public override void OnKill() {
            CWRPlayer modPlayer = Main.LocalPlayer.CWR();
            if (modPlayer.TETramContrType == WhoAmI) {
                modPlayer.SupertableUIStartBool = false;
                modPlayer.TETramContrType = -1;
            }
        }
    }
}

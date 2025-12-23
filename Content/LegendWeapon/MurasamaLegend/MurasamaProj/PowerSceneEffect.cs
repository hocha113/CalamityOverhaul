using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.MurasamaLegend.MurasamaProj
{
    internal class PowerSceneEffect : ModSystem
    {
        public override void PostSetupContent() {
            if (Main.LocalPlayer.CWR().InFoodStallChair) {
                Main.newMusic = Main.musicBox2 = MusicLoader.GetMusicSlot("CalamityOverhaul/Assets/Sounds/Music/BuryTheLight");
            }
        }
    }
}

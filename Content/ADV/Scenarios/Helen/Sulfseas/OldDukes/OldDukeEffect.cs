using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Helen.Sulfseas.OldDukes
{
    internal class OldDukeEffect : ModSystem
    {
        public static bool IsActive;
        public override void PostUpdateEverything() {
            if (IsActive) {
                Main.newMusic = Main.musicBox2 = MusicLoader.GetMusicSlot("CalamityModMusic/Sounds/Music/AcidRainTier1");
            }
        }
    }
}

using Terraria.ModLoader;

namespace CalamityOverhaul.OtherMods.HighFPSSupport
{
    internal class HighFPSRef : ICWRLoader
    {
        public static bool Has => ModLoader.HasMod("HighFPSSupport");
        void ICWRLoader.LoadData() {
            
        }
    }
}

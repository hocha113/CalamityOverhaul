using CalamityMod.Systems;
using System.Linq;
using Terraria.ModLoader;

namespace CalamityOverhaul
{
    internal class FixLavaRendering : ICWRLoader
    {
        void ICWRLoader.SetupData() {
            LavaRendering.instance.WaterStyleMaxCount = ModContent.GetContent<ModWaterStyle>().Count() + LoaderManager.Get<WaterStylesLoader>().VanillaCount;
        }
    }
}

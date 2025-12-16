using System.Linq;
using System.Reflection;
using Terraria.ModLoader;
using Terraria.ModLoader.Core;

namespace CalamityOverhaul.Common
{
    internal class FixLavaRendering : ICWRLoader
    {
        //对此我是无语的，就帮灾官擦一次屁股
        void ICWRLoader.SetupData() {
            if (CWRMod.Instance.calamity == null) {
                return;
            }
            var type = CWRUtils.GetTargetTypeInStringKey(AssemblyManager.GetLoadableTypes(CWRMod.Instance.calamity.Code), "LavaRendering");
            if (type == null) {
                return;
            }
            var fid = type.GetField("instance", BindingFlags.Static | BindingFlags.Public);
            if (fid == null) {
                return;
            }
            var fid2 = type.GetField("WaterStyleMaxCount", BindingFlags.Instance | BindingFlags.Public);
            if (fid2 == null) {
                return;
            }
            fid2.SetValue(fid.GetValue(null), ModContent.GetContent<ModWaterStyle>().Count() + LoaderManager.Get<WaterStylesLoader>().VanillaCount);
        }
    }
}

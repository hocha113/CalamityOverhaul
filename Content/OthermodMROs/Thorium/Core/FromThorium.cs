using System.Collections.Generic;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.OthermodMROs.Thorium.Core
{
    internal static class FromThorium
    {
        public const string Name = "ThoriumMod";
        public static bool Has => ModLoader.HasMod(Name);
        public static List<LThoriumCall> lThoriumCalls = [];
        public static void LoadData() {
            if (!Has) return;
            CWRMod.Instance.thoriumMod = ModLoader.GetMod(Name);
            lThoriumCalls = CWRUtils.GetSubInterface<LThoriumCall>("LThoriumCall");
            foreach (var call in lThoriumCalls) {
                call.LoadThoData(CWRMod.Instance.thoriumMod);
            }
        }

        public static void UnLoadData() {
            if (lThoriumCalls != null) {
                foreach (var call in lThoriumCalls) {
                    call.UnLoadThoData();
                }
            }
            lThoriumCalls = null;
        }

        public static void PostLoadData() {
            if (!Has) return;
            foreach (var call in lThoriumCalls) {
                call.PostLoadThoData(CWRMod.Instance.thoriumMod);
            }
        }
    }
}

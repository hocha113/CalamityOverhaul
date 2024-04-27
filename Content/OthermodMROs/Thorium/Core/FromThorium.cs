using System.Collections.Generic;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.OthermodMROs.Thorium.Core
{
    internal static class FromThorium
    {
        public const string Name = "ThoriumMod";
        public static bool Has => ModLoader.HasMod(Name);
        public static List<LThoriumCall> lThoriumCalls = new List<LThoriumCall>();
        public static void LoadDate() {
            if (!Has) return;
            CWRMod.Instance.thoriumMod = ModLoader.GetMod(Name);
            lThoriumCalls = CWRUtils.GetSubInterface<LThoriumCall>("LThoriumCall");
            foreach (var call in lThoriumCalls) {
                call.LoadThoDate(CWRMod.Instance.thoriumMod);
            }
        }

        public static void PostLoadDate() {
            if (!Has) return;
            foreach (var call in lThoriumCalls) {
                call.PostLoadThoDate(CWRMod.Instance.thoriumMod);
            }
        }
    }
}

using System.Collections.Generic;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.OthermodMROs.Thorium.Core
{
    internal static class FromThorium
    {
        public static bool Has => ModLoader.HasMod("ThoriumMod");
        public static List<LThoriumCall> lThoriumCalls = new List<LThoriumCall>();
        public static void LoadDate() {
            lThoriumCalls = CWRUtils.GetSubInterface<LThoriumCall>();
            foreach (var call in lThoriumCalls) {
                call.LoadThoDate(CWRMod.Instance.thoriumMod);
            }
        }
    }
}

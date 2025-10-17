using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.ADV
{
    internal static class ADVAsset
    {
        [VaultLoaden(CWRConstant.UI + "Halibut/")]
        public static Texture2D HeadADV = null;
        [VaultLoaden(CWRConstant.ADV + "SupCal/SupCal", startIndex: 1, arrayCount: 6)]
        public static IList<Texture2D> SupCalADV = null;
    }
}

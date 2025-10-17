﻿using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.ADV
{
    internal static class ADVAsset
    {
        [VaultLoaden(CWRConstant.ADV + "Halibut/Helen")]
        public static Texture2D HelenADV = null;
        [VaultLoaden(CWRConstant.ADV + "SupCal/SupCal", startIndex: 1, arrayCount: 7)]
        public static IList<Texture2D> SupCalADV = null;
    }
}

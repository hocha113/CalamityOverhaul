using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;

namespace CalamityOverhaul.Content.ADV
{
    internal static class ADVAsset
    {
        [VaultLoaden(CWRConstant.ADV + "Halibut/Helen")]
        public static Texture2D HelenADV = null;
        [VaultLoaden(CWRConstant.ADV + "Halibut/Helen_amaze")]
        public static Texture2D Helen_amazeADV = null;
        [VaultLoaden(CWRConstant.ADV + "Halibut/Helen_solemn")]
        public static Texture2D Helen_solemnADV = null;
        [VaultLoaden(CWRConstant.ADV + "Halibut/Helen_wrath")]
        public static Texture2D Helen_wrathADV = null;
        [VaultLoaden(CWRConstant.ADV + "Halibut/Helen_doubt")]
        public static Texture2D Helen_doubtADV = null;
        [VaultLoaden(CWRConstant.ADV + "Halibut/Helen_enjoy")]
        public static Texture2D Helen_enjoyADV = null;
        [VaultLoaden(CWRConstant.ADV + "Halibut/Helen_enjoy2")]
        public static Texture2D Helen_enjoy2ADV = null;
        [VaultLoaden(CWRConstant.ADV + "Halibut/Helen_enjoy3")]
        public static Texture2D Helen_enjoy3ADV = null;
        [VaultLoaden(CWRConstant.ADV + "Halibut/Helen_slightAnnoyed")]
        public static Texture2D Helen_slightAnnoyedADV = null;
        [VaultLoaden(CWRConstant.ADV + "Halibut/Helen_wail")]
        public static Texture2D Helen_wailADV = null;
        [VaultLoaden(CWRConstant.ADV + "SupCal/SupCal", startIndex: 0, arrayCount: 6)]
        public static IList<Texture2D> SupCalADV = null;
    }
}

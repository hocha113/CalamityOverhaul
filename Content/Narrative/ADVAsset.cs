using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;

namespace CalamityOverhaul.Content.Narrative
{
    public static class ADVAsset
    {
        [VaultLoaden(CWRConstant.ADV + "SupCal/SupCal", startIndex: 0, arrayCount: 6)]
        public static IList<Texture2D> SupCalsADV = null;
        [VaultLoaden(CWRConstant.ADV + "SupCal/SupCal")]
        public static Texture2D SupCalADV = null;
        [VaultLoaden(CWRConstant.ADV + "SupCal/")]
        public static Texture2D Lain = null;
        [VaultLoaden(CWRConstant.ADV + "SupCal/")]
        public static Texture2D Lain_smile = null;
        [VaultLoaden(CWRConstant.ADV + "SupCal/SupCal_closeEyes")]
        public static Texture2D SupCal_closeEyesADV = null;
        [VaultLoaden(CWRConstant.ADV + "SupCal/SupCal_smile")]
        public static Texture2D SupCal_smileADV = null;
        [VaultLoaden(CWRConstant.ADV + "Halibut/Helen")]
        public static Texture2D HelenADV = null;
        [VaultLoaden(CWRConstant.ADV + "Halibut/Helen2")]
        public static Texture2D Helen2ADV = null;
        [VaultLoaden(CWRConstant.ADV + "Halibut/Helen_silence")]
        public static Texture2D Helen_silenceADV = null;
        [VaultLoaden(CWRConstant.ADV + "Halibut/Helen_naughty")]
        public static Texture2D Helen_naughtyADV = null;
        [VaultLoaden(CWRConstant.ADV + "Halibut/Helen_naughty2")]
        public static Texture2D Helen_naughty2ADV = null;
        [VaultLoaden(CWRConstant.ADV + "Halibut/Helen_serious2")]
        public static Texture2D Helen_serious2ADV = null;
        [VaultLoaden(CWRConstant.ADV + "Halibut/Helen_tired")]
        public static Texture2D Helen_tiredADV = null;
        [VaultLoaden(CWRConstant.ADV + "Halibut/Helen_amaze")]
        public static Texture2D Helen_amazeADV = null;
        [VaultLoaden(CWRConstant.ADV + "Halibut/Helen_solemn")]
        public static Texture2D Helen_solemnADV = null;
        [VaultLoaden(CWRConstant.ADV + "Halibut/Helen_serious")]
        public static Texture2D Helen_seriousADV = null;
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
        [VaultLoaden(CWRConstant.ADV + "Draedon/Draedon")]
        public static Texture2D DraedonADV = null;
        [VaultLoaden(CWRConstant.ADV + "Draedon/Draedon2")]
        public static Texture2D Draedon2ADV = null;
        [VaultLoaden(CWRConstant.ADV + "Draedon/DraedonRed")]
        public static Texture2D DraedonRedADV = null;
        [VaultLoaden(CWRConstant.ADV + "Draedon/Draedon2Red")]
        public static Texture2D Draedon2RedADV = null;
        [VaultLoaden(CWRConstant.ADV + "Draedon/Tzeentch")]
        public static Texture2D Tzeentch = null;
        [VaultLoaden(CWRConstant.ADV + "Draedon/")]
        public static Texture2D Apollia = null;
        [VaultLoaden(CWRConstant.ADV + "Draedon/")]
        public static Texture2D Apollia_Calmnessl = null;
        [VaultLoaden(CWRConstant.ADV + "Draedon/")]
        public static Texture2D Apollia_Feel = null;
        [VaultLoaden(CWRConstant.ADV + "Draedon/")]
        public static Texture2D Apollia_Rage = null;
        [VaultLoaden(CWRConstant.ADV + "Draedon/")]
        public static Texture2D Apollia_Worry = null;
        [VaultLoaden(CWRConstant.ADV + "Draedon/")]
        public static Texture2D ApolliaIcon = null;
        [VaultLoaden(CWRConstant.ADV + "Draedon/")]
        public static Texture2D ApolliaActor = null;
        [VaultLoaden(CWRConstant.ADV + "Draedon/")]
        public static Texture2D ApolliaActor_Jump = null;
        [VaultLoaden(CWRConstant.ADV + "Draedon/")]
        public static Texture2D Artis = null;
        [VaultLoaden(CWRConstant.ADV)]
        public static Texture2D FUJI = null;
        [VaultLoaden(CWRConstant.ADV + "Abysse/")]
        public static Texture2D Floatsland = null;
        [VaultLoaden(CWRConstant.ADV + "Shepel/")]
        public static Texture2D Shepel = null;
        [VaultLoaden(CWRConstant.ADV + "Shepel/")]
        public static Texture2D Shepel_Blank = null;
        [VaultLoaden(CWRConstant.ADV + "Shepel/")]
        public static Texture2D Shepel_Happy = null;
        [VaultLoaden(CWRConstant.ADV + "Shepel/")]
        public static Texture2D Shepel_Pain = null;
        [VaultLoaden(CWRConstant.ADV + "Shepel/")]
        public static Texture2D Shepel_Sad = null;
        [VaultLoaden(CWRConstant.ADV + "Shepel/")]
        public static Texture2D Shepel_Serious = null;
        [VaultLoaden(CWRConstant.ADV + "Shepel/")]
        public static Texture2D Shepel_Shocked = null;
        [VaultLoaden(CWRConstant.ADV + "Shepel/")]
        public static Texture2D Shepel_Sleep = null;
        [VaultLoaden(CWRConstant.ADV + "Shepel/")]
        public static Texture2D Shepel_Smirk = null;
        [VaultLoaden(CWRConstant.ADV + "Himayo/")]
        public static Texture2D Himayo = null;
        [VaultLoaden(CWRConstant.ADV + "Himayo/")]
        public static Texture2D Himayo_doubt = null;
        [VaultLoaden(CWRConstant.ADV + "Himayo/")]
        public static Texture2D Himayo_grin = null;
        [VaultLoaden(CWRConstant.ADV + "Himayo/")]
        public static Texture2D Himayo_forsmile = null;
        [VaultLoaden(CWRConstant.ADV + "Himayo/")]
        public static Texture2D Himayo_ruminate = null;
        [VaultLoaden(CWRConstant.ADV + "VoidColony/")]
        public static Texture2D Glitchwraith = null;
    }
}

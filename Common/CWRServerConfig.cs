using System.ComponentModel;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader.Config;

namespace CalamityOverhaul.Common
{
    [BackgroundColor(49, 32, 36, 216)]
    public class CWRServerConfig : ModConfig
    {
        public static CWRServerConfig Instance { get; private set; }

        public override ConfigScope Mode => ConfigScope.ServerSide;

        private static class Date
        {
            internal const float MScaleOffset_MinValue = 0.2f;
            internal const float MScaleOffset_MaxValue = 1f;
            internal const float M_RDCD_BarSize_MinValue = 0.5f;
            internal const float M_RDCD_BarSize_MaxValue = 2f;
            public static int CartridgeUI_Offset_X;
            internal const int CartridgeUI_Offset_X_MinValue = 0;
            internal const int CartridgeUI_Offset_X_MaxValue = 1900;
            public static int CartridgeUI_Offset_Y;
            internal const int CartridgeUI_Offset_Y_MinValue = 0;
            internal const int CartridgeUI_Offset_Y_MaxValue = 800;
            public static float LoadingAA_VolumeValue;
            internal const int LoadingAA_Volume_MinValue = 0;
            internal const int LoadingAA_Volume_MaxValue = 800;
            internal const int MuraUIStyleMaxType = 4;
            internal const int MuraUIStyleMinType = 1;
            public static int MuraUIStyleValue;
            internal const int MuraPosStyleMaxType = 3;
            internal const int MuraPosStyleMinType = 1;
            public static int MuraPosStyleValue;
        }

        [Header("CWRSystem")]

        [BackgroundColor(35, 185, 78, 255)]
        [ReloadRequired]
        [DefaultValue(true)]
        public bool ForceReplaceResetContent { get; set; }//是否开启强制内容替换

        [BackgroundColor(35, 185, 78, 255)]
        [ReloadRequired]
        [DefaultValue(true)]
        public bool BiobehavioralOverlay { get; set; }

        [BackgroundColor(35, 185, 78, 255)]
        [DefaultValue(true)]
        public bool WeaponEnhancementSystem { get; set; }

        [Header("CWRWeapon")]

        [BackgroundColor(192, 54, 94, 255)]
        [DefaultValue(true)]
        public bool WeaponHandheldDisplay { get; set; }

        [BackgroundColor(192, 54, 94, 255)]
        [DefaultValue(true)]
        public bool EnableSwordLight { get; set; }

        [BackgroundColor(192, 54, 94, 255)]
        [DefaultValue(true)]
        public bool ActivateGunRecoil { get; set; }

        [BackgroundColor(192, 54, 94, 255)]
        [DefaultValue(true)]
        public bool MagazineSystem { get; set; }

        [BackgroundColor(192, 54, 94, 255)]
        [DefaultValue(true)]
        public bool EnableCasingsEntity { get; set; }

        [BackgroundColor(192, 54, 94, 255)]
        [DefaultValue(true)]
        public bool BowArrowDraw { get; set; }

        [BackgroundColor(192, 54, 94, 255)]
        [DefaultValue(true)]
        public bool WeaponAdaptiveIllumination { get; set; }

        [BackgroundColor(192, 54, 94, 255)]
        [DefaultValue(true)]
        public bool WeaponAdaptiveVolumeScaling { get; set; }

        [BackgroundColor(192, 54, 94, 255)]
        [DefaultValue(false)]
        public bool ShotgunFireForcedReloadInterruption { get; set; }

        [BackgroundColor(192, 54, 94, 255)]
        [DefaultValue(true)]
        public bool ScreenVibration { get; set; }//武器屏幕振动

        [BackgroundColor(192, 54, 94, 255)]
        [DefaultValue(true)]
        public bool MurasamaSpaceFragmentationBool { get; set; }//鬼妖终结技碎屏效果

        [BackgroundColor(192, 54, 94, 255)]
        [SliderColor(224, 165, 56, 255)]
        [Range(Date.LoadingAA_Volume_MinValue, Date.LoadingAA_Volume_MaxValue)]
        [DefaultValue(1)]
        public float LoadingAA_Volume {
            get {
                if (Date.LoadingAA_VolumeValue < Date.LoadingAA_Volume_MinValue) {
                    Date.LoadingAA_VolumeValue = Date.LoadingAA_Volume_MinValue;
                }
                if (Date.LoadingAA_VolumeValue > Date.LoadingAA_Volume_MaxValue) {
                    Date.LoadingAA_VolumeValue = Date.LoadingAA_Volume_MaxValue;
                }
                return Date.LoadingAA_VolumeValue;
            }
            set => Date.LoadingAA_VolumeValue = value;
        }

        [BackgroundColor(192, 54, 94, 255)]
        [DefaultValue(true)]
        public bool LensEasing { get; set; }//镜头缓动

        [Header("CWRUI")]

        [BackgroundColor(45, 175, 225, 255)]
        [SliderColor(224, 165, 56, 255)]
        [Range(Date.MuraUIStyleMinType, Date.MuraUIStyleMaxType)]
        [DefaultValue(1)]
        public int MuraUIStyleType {
            get {
                if (Date.MuraUIStyleValue < Date.MuraUIStyleMinType) {
                    Date.MuraUIStyleValue = Date.MuraUIStyleMinType;
                }
                if (Date.MuraUIStyleValue > Date.MuraUIStyleMaxType) {
                    Date.MuraUIStyleValue = Date.MuraUIStyleMaxType;
                }
                return Date.MuraUIStyleValue;
            }
            set => Date.MuraUIStyleValue = value;
        }

        [BackgroundColor(45, 175, 225, 255)]
        [SliderColor(224, 165, 56, 255)]
        [Range(Date.MuraPosStyleMinType, Date.MuraPosStyleMaxType)]
        [DefaultValue(1)]
        public int MuraPosStyleType {
            get {
                if (Date.MuraPosStyleValue < Date.MuraPosStyleMinType) {
                    Date.MuraPosStyleValue = Date.MuraPosStyleMinType;
                }
                if (Date.MuraPosStyleValue > Date.MuraPosStyleMaxType) {
                    Date.MuraPosStyleValue = Date.MuraPosStyleMaxType;
                }
                return Date.MuraPosStyleValue;
            }
            set => Date.MuraPosStyleValue = value;
        }

        [BackgroundColor(45, 175, 225, 255)]
        [SliderColor(224, 165, 56, 255)]
        [Range(Date.CartridgeUI_Offset_X_MinValue, Date.CartridgeUI_Offset_X_MaxValue)]
        [DefaultValue(1)]
        public int CartridgeUI_Offset_X_Value {//弹夹UI位置调节_X
            get {
                if (Date.CartridgeUI_Offset_X < Date.CartridgeUI_Offset_X_MinValue) {
                    Date.CartridgeUI_Offset_X = Date.CartridgeUI_Offset_X_MinValue;
                }
                if (Date.CartridgeUI_Offset_X > Date.CartridgeUI_Offset_X_MaxValue) {
                    Date.CartridgeUI_Offset_X = Date.CartridgeUI_Offset_X_MaxValue;
                }
                return Date.CartridgeUI_Offset_X;
            }
            set => Date.CartridgeUI_Offset_X = value;
        }

        [BackgroundColor(45, 175, 225, 255)]
        [SliderColor(224, 165, 56, 255)]
        [Range(Date.CartridgeUI_Offset_Y_MinValue, Date.CartridgeUI_Offset_Y_MaxValue)]
        [DefaultValue(1)]
        public int CartridgeUI_Offset_Y_Value {//弹夹UI位置调节_Y
            get {
                if (Date.CartridgeUI_Offset_Y < Date.CartridgeUI_Offset_Y_MinValue) {
                    Date.CartridgeUI_Offset_Y = Date.CartridgeUI_Offset_Y_MinValue;
                }
                if (Date.CartridgeUI_Offset_Y > Date.CartridgeUI_Offset_Y_MaxValue) {
                    Date.CartridgeUI_Offset_Y = Date.CartridgeUI_Offset_Y_MaxValue;
                }
                return Date.CartridgeUI_Offset_Y;
            }
            set => Date.CartridgeUI_Offset_Y = value;
        }

        public override void OnLoaded() => Instance = this;

        public override bool AcceptClientChanges(ModConfig pendingConfig, int whoAmI, ref NetworkText message) {
            string text = CWRLocText.GetTextValue("Config_1")
                + Main.player[whoAmI].name + CWRLocText.GetTextValue("Config_2");
            CWRUtils.Text(text);
            return true;
        }
    }
}

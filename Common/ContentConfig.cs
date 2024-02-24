using System.ComponentModel;
using Terraria.ModLoader.Config;

namespace CalamityOverhaul.Common
{
    [BackgroundColor(49, 32, 36, 216)]
    public class ContentConfig : ModConfig
    {
        public static ContentConfig Instance;

        public override ConfigScope Mode => ConfigScope.ServerSide;

        private static class Date{
            internal const float MScaleOffset_MinValue = 0.2f;
            internal const float MScaleOffset_MaxValue = 1f;
            public static float MScaleOffsetValue;
            internal const float M_RDCD_BarSize_MinValue = 0.5f;
            internal const float M_RDCD_BarSize_MaxValue = 2f;
            public static float M_RDCD_BarSizeValue;
        }

        public float GetDateMScaleOffsetValue() => Date.MScaleOffsetValue;

        [Header("CWRSystem")]

        /// <summary>
        /// 是否开启强制内容替换
        /// </summary>
        [BackgroundColor(192, 54, 64, 192)]
        [ReloadRequired]
        [DefaultValue(true)]
        public bool ForceReplaceResetContent { get; set; }
        /// <summary>
        /// 传奇武器系统
        /// </summary>
        [BackgroundColor(192, 54, 64, 192)]
        [ReloadRequired]
        [DefaultValue(true)]
        public bool WeaponEnhancementSystem { get; set; }
        /// <summary>
        /// 纹理资源替换
        /// </summary>
        [BackgroundColor(192, 54, 64, 192)]
        [ReloadRequired]
        [DefaultValue(true)]
        public bool ReplaceAsset { get; set; }
        /// <summary>
        /// 重置物品的温馨提示
        /// </summary>
        [BackgroundColor(192, 54, 64, 192)]
        [DefaultValue(true)]
        public bool ResetItemReminder { get; set; }

        [Header("CWRWeapon")]

        /// <summary>
        /// 鬼妖屏幕振动
        /// </summary>
        [BackgroundColor(182, 24, 64, 192)]
        [DefaultValue(false)]
        public bool MurasamaScreenVibration { get; set; }
        /// <summary>
        /// 鬼妖终结技碎屏效果
        /// </summary>
        [BackgroundColor(182, 24, 64, 192)]
        [DefaultValue(true)]
        public bool MurasamaSpaceFragmentationBool { get; set; }
        /// <summary>
        /// 鬼妖刀刃大小调节
        /// </summary>
        [BackgroundColor(182, 24, 64, 192)]
        [SliderColor(224, 165, 56, 128)]
        [Range(Date.MScaleOffset_MinValue, Date.MScaleOffset_MaxValue)]
        [DefaultValue(1)]
        public float MurasamaScaleOffset {
            get {
                if (Date.MScaleOffsetValue < Date.MScaleOffset_MinValue) {
                    Date.MScaleOffsetValue = Date.MScaleOffset_MinValue;
                }
                if (Date.MScaleOffsetValue > Date.MScaleOffset_MaxValue) {
                    Date.MScaleOffsetValue = Date.MScaleOffset_MaxValue;
                }
                return Date.MScaleOffsetValue;
            }
            set => Date.MScaleOffsetValue = value;
        }
        /// <summary>
        /// 镜头缓动
        /// </summary>
        [BackgroundColor(182, 24, 64, 192)]
        [DefaultValue(true)]
        public bool LensEasing { get; set; }

        [Header("CWRUI")]

        /// <summary>
        /// 鬼妖升龙冷却UI大小调节
        /// </summary>
        [BackgroundColor(182, 24, 64, 192)]
        [SliderColor(224, 165, 56, 128)]
        [Range(Date.M_RDCD_BarSize_MinValue, Date.M_RDCD_BarSize_MaxValue)]
        [DefaultValue(1)]
        public float MurasamaRisingDragonCoolDownBarSize {
            get {
                if (Date.M_RDCD_BarSizeValue < Date.M_RDCD_BarSize_MinValue) {
                    Date.M_RDCD_BarSizeValue = Date.M_RDCD_BarSize_MinValue;
                }
                if (Date.M_RDCD_BarSizeValue > Date.M_RDCD_BarSize_MaxValue) {
                    Date.M_RDCD_BarSizeValue = Date.M_RDCD_BarSize_MaxValue;
                }
                return Date.M_RDCD_BarSizeValue;
            }
            set => Date.M_RDCD_BarSizeValue = value;
        }

        public override void OnLoaded() => Instance = this;

        [System.Obsolete]
        public override bool AcceptClientChanges(ModConfig pendingConfig, int whoAmI, ref string message) {
            return true;
        }
    }
}

using CalamityOverhaul.Content.UIs.OverhaulSettings;
using System.ComponentModel;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader.Config;

namespace CalamityOverhaul.Common
{
    [BackgroundColor(49, 32, 36, 216)]
    public class CWRServerConfig : ModConfig
    {
        //提醒自己不要用懒加载
        public static CWRServerConfig Instance { get; private set; }
        public override ConfigScope Mode => ConfigScope.ServerSide;
        private static class Data
        {
            internal const float MScaleOffset_MinValue = 0.2f;
            internal const float MScaleOffset_MaxValue = 1f;
            internal const float M_RDCD_BarSize_MinValue = 0.5f;
            internal const float M_RDCD_BarSize_MaxValue = 2f;
            internal const int MuraUIStyleMaxType = 5;
            internal const int MuraUIStyleMinType = 1;
            public static int MuraUIStyleValue;
            internal const int MuraPosStyleMaxType = 3;
            internal const int MuraPosStyleMinType = 1;
            public static int MuraPosStyleValue;
            /// <summary>
            /// 旧的手持开关，用于对比检测手持是否改变设置
            /// </summary>
            internal static bool OldWeaponHandheldDisplay;
        }

        [Header("CWRSystem")]

        [BackgroundColor(35, 185, 78, 255)]
        [ReloadRequired]
        [DefaultValue(true)]
        public bool QuestLog { get; set; }//是否开启任务书系统

        [BackgroundColor(35, 185, 78, 255)]
        [ReloadRequired]
        [DefaultValue(true)]
        public bool BiologyOverhaul { get; set; }

        [Header("CWRWeapon")]

        [BackgroundColor(192, 54, 94, 255)]
        [DefaultValue(true)]
        public bool WeaponHandheldDisplay { get; set; }

        [BackgroundColor(192, 54, 94, 255)]
        [DefaultValue(true)]
        public bool EnableSwordLight { get; set; }

        [BackgroundColor(192, 54, 94, 255)]
        [DefaultValue(true)]
        public bool EnableCasingsEntity { get; set; }

        [BackgroundColor(192, 54, 94, 255)]
        [DefaultValue(true)]
        public bool BowArrowDraw { get; set; }

        [BackgroundColor(192, 54, 94, 255)]
        [DefaultValue(false)]
        public bool WeaponLazyRotationAngle { get; set; }

        [BackgroundColor(192, 54, 94, 255)]
        [DefaultValue(true)]
        public bool ScreenVibration { get; set; }//武器屏幕振动

        [BackgroundColor(192, 54, 94, 255)]
        [DefaultValue(true)]
        public bool MurasamaSpaceFragmentationBool { get; set; }//鬼妖终结技碎屏效果

        [BackgroundColor(192, 54, 94, 255)]
        [DefaultValue(false)]
        public bool DomainConciseDisplay { get; set; }//领域简洁显示

        [BackgroundColor(192, 54, 94, 255)]
        [DefaultValue(true)]
        public bool LensEasing { get; set; }//镜头缓动

        [Header("CWRWorldGen")]

        [BackgroundColor(100, 160, 80, 255)]
        [DefaultValue(true)]
        public bool GenWindGrivenGenerator { get; set; }

        [BackgroundColor(100, 160, 80, 255)]
        [DefaultValue(true)]
        public bool GenWGGCollector { get; set; }

        [BackgroundColor(100, 160, 80, 255)]
        [DefaultValue(true)]
        public bool GenJunkmanBase { get; set; }

        [BackgroundColor(100, 160, 80, 255)]
        [DefaultValue(true)]
        public bool GenRocketHut { get; set; }

        [BackgroundColor(100, 160, 80, 255)]
        [DefaultValue(true)]
        public bool GenSylvanOutpost { get; set; }

        [Header("CWRUI")]

        [BackgroundColor(45, 175, 225, 255)]
        [SliderColor(224, 165, 56, 255)]
        [Range(Data.MuraUIStyleMinType, Data.MuraUIStyleMaxType)]
        [DefaultValue(1)]
        public int MuraUIStyleType {
            get {
                if (Data.MuraUIStyleValue < Data.MuraUIStyleMinType) {
                    Data.MuraUIStyleValue = Data.MuraUIStyleMinType;
                }
                if (Data.MuraUIStyleValue > Data.MuraUIStyleMaxType) {
                    Data.MuraUIStyleValue = Data.MuraUIStyleMaxType;
                }
                return Data.MuraUIStyleValue;
            }
            set => Data.MuraUIStyleValue = value;
        }

        [BackgroundColor(45, 175, 225, 255)]
        [SliderColor(224, 165, 56, 255)]
        [Range(Data.MuraPosStyleMinType, Data.MuraPosStyleMaxType)]
        [DefaultValue(1)]
        public int MuraPosStyleType {
            get {
                if (Data.MuraPosStyleValue < Data.MuraPosStyleMinType) {
                    Data.MuraPosStyleValue = Data.MuraPosStyleMinType;
                }
                if (Data.MuraPosStyleValue > Data.MuraPosStyleMaxType) {
                    Data.MuraPosStyleValue = Data.MuraPosStyleMaxType;
                }
                return Data.MuraPosStyleValue;
            }
            set => Data.MuraPosStyleValue = value;
        }

        [BackgroundColor(45, 175, 225, 255)]
        [SliderColor(224, 165, 56, 255)]
        [Range(1f, 2f)]
        [DefaultValue(1)]
        public float DialogueBox_Scale_Value { get; set; }//对话框缩放比例

        [BackgroundColor(45, 175, 225, 255)]
        [DefaultValue(false)]
        public bool SignalTowerDecryptionAutoPlay { get; set; }//信号塔破译傻瓜模式：开启后小游戏自动演示正确解答

        public override void OnLoaded() {
            Instance = this;
            Data.OldWeaponHandheldDisplay = WeaponHandheldDisplay;
        }

        public override void OnChanged() {
            if (!VaultLoad.LoadenContent) {
                return;
            }
            WorldGenDensitySave.SyncFromConfig();
        }

        public override bool AcceptClientChanges(ModConfig pendingConfig, int whoAmI, ref NetworkText message) {
            string text = CWRLocText.GetTextValue("Config_1")
                + Main.player[whoAmI].name + CWRLocText.GetTextValue("Config_2");
            VaultUtils.Text(text);
            return true;
        }
    }
}

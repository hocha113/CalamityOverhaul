using CalamityOverhaul.Content.RangedModify.Core;
using System.ComponentModel;
using Terraria;
using Terraria.ID;
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
            public static int CartridgeUI_Offset_X;
            internal const int CartridgeUI_Offset_X_MinValue = 0;
            internal const int CartridgeUI_Offset_X_MaxValue = 1900;
            public static int CartridgeUI_Offset_Y;
            internal const int CartridgeUI_Offset_Y_MinValue = 0;
            internal const int CartridgeUI_Offset_Y_MaxValue = 800;
            public static float LoadingAA_VolumeValue;
            internal const int LoadingAA_Volume_MinValue = 0;
            internal const int LoadingAA_Volume_MaxValue = 800;
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
            /// <summary>
            /// 旧的弹匣开关，用于对比检测弹匣是否改变设置
            /// </summary>
            internal static bool OldMagazineSystem;
        }

        [Header("CWRSystem")]

        [BackgroundColor(35, 185, 78, 255)]
        [ReloadRequired]
        [DefaultValue(true)]
        public bool WeaponOverhaul { get; set; }//是否开启强制内容替换

        [BackgroundColor(35, 185, 78, 255)]
        [ReloadRequired]
        [DefaultValue(true)]
        public bool BiologyOverhaul { get; set; }

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
        [DefaultValue(false)]
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
        [DefaultValue(false)]
        public bool ShotgunFireForcedReloadInterruption { get; set; }

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
        [SliderColor(224, 165, 56, 255)]
        [Range(Data.LoadingAA_Volume_MinValue, Data.LoadingAA_Volume_MaxValue)]
        [DefaultValue(1)]
        public float LoadingAA_Volume {
            get {
                if (Data.LoadingAA_VolumeValue < Data.LoadingAA_Volume_MinValue) {
                    Data.LoadingAA_VolumeValue = Data.LoadingAA_Volume_MinValue;
                }
                if (Data.LoadingAA_VolumeValue > Data.LoadingAA_Volume_MaxValue) {
                    Data.LoadingAA_VolumeValue = Data.LoadingAA_Volume_MaxValue;
                }
                return Data.LoadingAA_VolumeValue;
            }
            set => Data.LoadingAA_VolumeValue = value;
        }

        [BackgroundColor(192, 54, 94, 255)]
        [DefaultValue(true)]
        public bool LensEasing { get; set; }//镜头缓动

        [Header("CWRUI")]

        [BackgroundColor(45, 175, 225, 255)]
        [DefaultValue(false)]
        public bool ShowReloadingProgressUI { get; set; }

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
        [Range(Data.CartridgeUI_Offset_X_MinValue, Data.CartridgeUI_Offset_X_MaxValue)]
        [DefaultValue(1)]
        public int CartridgeUI_Offset_X_Value {//弹夹UI位置调节_X
            get {
                if (Data.CartridgeUI_Offset_X < Data.CartridgeUI_Offset_X_MinValue) {
                    Data.CartridgeUI_Offset_X = Data.CartridgeUI_Offset_X_MinValue;
                }
                if (Data.CartridgeUI_Offset_X > Data.CartridgeUI_Offset_X_MaxValue) {
                    Data.CartridgeUI_Offset_X = Data.CartridgeUI_Offset_X_MaxValue;
                }
                return Data.CartridgeUI_Offset_X;
            }
            set => Data.CartridgeUI_Offset_X = value;
        }

        [BackgroundColor(45, 175, 225, 255)]
        [SliderColor(224, 165, 56, 255)]
        [Range(Data.CartridgeUI_Offset_Y_MinValue, Data.CartridgeUI_Offset_Y_MaxValue)]
        [DefaultValue(1)]
        public int CartridgeUI_Offset_Y_Value {//弹夹UI位置调节_Y
            get {
                if (Data.CartridgeUI_Offset_Y < Data.CartridgeUI_Offset_Y_MinValue) {
                    Data.CartridgeUI_Offset_Y = Data.CartridgeUI_Offset_Y_MinValue;
                }
                if (Data.CartridgeUI_Offset_Y > Data.CartridgeUI_Offset_Y_MaxValue) {
                    Data.CartridgeUI_Offset_Y = Data.CartridgeUI_Offset_Y_MaxValue;
                }
                return Data.CartridgeUI_Offset_Y;
            }
            set => Data.CartridgeUI_Offset_Y = value;
        }

        public override void OnLoaded() {
            Instance = this;
            Data.OldWeaponHandheldDisplay = WeaponHandheldDisplay;
            Data.OldMagazineSystem = MagazineSystem;
        }

        public override void OnChanged() {
            if (Main.gameMenu) {
                return;
            }
            //事实证明这个东西可能是不安全的，重新设置一个物品可能会导致它的一些属性变成null或者是其他错误的值，
            //这个情况的来源是他人所编写的不安全代码，总之这是危险代码，只能注释并阉割功能
            //ChangedPlayerItem();
            ChangedRangedProperty();
        }

        //private void ChangedPlayerItem() {
        //   if (Main.LocalPlayer == null || Main.LocalPlayer.inventory == null) {
        //       return;
        //   }

        //   bool canSet = Data.OldWeaponHandheldDisplay != WeaponHandheldDisplay;
        //   Data.OldWeaponHandheldDisplay = WeaponHandheldDisplay;

        //   if (!canSet) {
        //       return;
        //   }

        //   foreach (var item in Main.LocalPlayer.inventory) {
        //       if (item == null || item.IsAir || !ItemOverride.ByID.ContainsKey(item.type)) {
        //           continue;
        //       }
        //       item.SetDefaults(item.type);
        //   }
        //}

        private void ChangedRangedProperty() {
            if (Main.projectile == null) {
                return;
            }

            bool canSet = Data.OldMagazineSystem != MagazineSystem;
            Data.OldMagazineSystem = MagazineSystem;

            if (!canSet) {
                return;
            }

            foreach (var proj in Main.ActiveProjectiles) {
                if (proj.hostile || proj.ModProjectile == null) {
                    continue;
                }
                if (proj.ModProjectile is BaseFeederGun gun) {
                    Item item = Main.player[proj.owner].GetItem();
                    if (item.type != ItemID.None) {
                        item.CWR().InitializeMagazine();
                    }
                    gun.SetRangedProperty();
                }
            }
        }

        public override bool AcceptClientChanges(ModConfig pendingConfig, int whoAmI, ref NetworkText message) {
            string text = CWRLocText.GetTextValue("Config_1")
                + Main.player[whoAmI].name + CWRLocText.GetTextValue("Config_2");
            VaultUtils.Text(text);
            return true;
        }
    }
}

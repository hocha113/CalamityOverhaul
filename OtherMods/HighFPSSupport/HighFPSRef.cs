using CalamityOverhaul.Content.Projectiles.Others;
using CalamityOverhaul.Content.UIs.OverhaulSettings;
using System;
using System.Reflection;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;

namespace CalamityOverhaul.OtherMods.HighFPSSupport
{
    internal class HighFPSRef : ModSystem, ILocalizedModType
    {
        public string LocalizationCategory => "Compatibility";

        public static LocalizedText DisableMotionInterpolationMessage { get; private set; }

        public static bool Has => CWRMod.Instance.highFPSSupport != null;
        private static MethodInfo DisableMotionInterpolationMethod;
        private static FieldInfo motionInterpolationField;

        public override void SetStaticDefaults() {
            DisableMotionInterpolationMessage = this.GetLocalization(nameof(DisableMotionInterpolationMessage), () =>
                """
                检测到您启用了HighFPSSupport:[运动流畅化]功能
                已经自动为您禁用，以保证模组的部分视觉效果运行稳定
                为了确保更改生效，强烈建议您手动切换并保持此功能的关闭
                """);
        }

        public override void Load() {
            if (!Has) {
                return;
            }
            try {
                var types = CWRUtils.GetModTypes(CWRMod.Instance.highFPSSupport);
                var configType = CWRUtils.GetTargetTypeInStringKey(types, "Config");
                motionInterpolationField = configType.GetField("motionInterpolation", BindingFlags.Public | BindingFlags.Instance);
                DisableMotionInterpolationMethod = configType.GetMethod("DisableMotionInterpolation", BindingFlags.NonPublic | BindingFlags.Static);
            } catch (Exception ex) { CWRMod.Instance.Logger.Error($"HighFPSRef.LoadData An Error Has Cccurred: {ex.Message}"); }
        }

        public override void Unload() {
            DisableMotionInterpolationMethod = null;
            motionInterpolationField = null;
        }

        /// <summary>
        /// 获取高FPS支持模组中运动插值功能的开启状态
        /// </summary>
        public static bool GetMotionInterpolationValue() {
            if (!Has || motionInterpolationField == null) {
                return false;
            }

            try {
                if (CWRMod.Instance.highFPSSupport.TryFind<ModConfig>("Config", out var config)) {
                    return (bool)motionInterpolationField.GetValue(config);
                }
            } catch (Exception ex) {
                CWRMod.Instance.Logger.Error($"HighFPSRef.GetMotionInterpolationValue An Error Has Cccurred: {ex.Message}");
                return false;
            }

            return false;
        }

        /// <summary>
        /// 禁用高FPS支持模组中的运动插值功能
        /// </summary>
        public static void DisableMotionInterpolation() {
            if (!Has) {
                return;
            }
            if (!CompatibilitySettingsSave.AutoDisableHighFPSMotionInterpolation) {
                return;
            }
            if (!GetMotionInterpolationValue()) {
                return;
            }
            SpwanTextProj.New(Main.LocalPlayer, () =>
                    VaultUtils.Text(DisableMotionInterpolationMessage.Value, Color.OrangeRed), 260);
            CWRMod.Instance.Logger.Info(DisableMotionInterpolationMessage.Value);
            DisableMotionInterpolationMethod?.Invoke(null, null);
        }
    }
}

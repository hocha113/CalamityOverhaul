using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Others;
using CalamityOverhaul.Content.UIs.OverhaulSettings;
using System;
using System.Reflection;
using Terraria;
using Terraria.ModLoader.Config;

namespace CalamityOverhaul.OtherMods.HighFPSSupport
{
    internal class HighFPSRef : ICWRLoader
    {
        public static bool Has => CWRMod.Instance.highFPSSupport != null;
        private static MethodInfo DisableMotionInterpolationMethod;
        private static FieldInfo motionInterpolationField;
        void ICWRLoader.LoadData() {
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
        void ICWRLoader.UnLoadData() {
            DisableMotionInterpolationMethod = null;
            motionInterpolationField = null;
        }
        /// <summary>
        /// 获取高FPS支持模组中运动插值功能的开启状态
        /// </summary>
        /// <returns></returns>
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
                return;//已经禁用了的话就不管了
            }
            //这里为了保证模组稳定，自动关闭运动插值功能
            SpwanTextProj.New(Main.LocalPlayer, () =>
                    VaultUtils.Text(CWRLocText.Instance.DisableMotionInterpolationMessage.Value
                    , Color.OrangeRed), 260
                );
            CWRMod.Instance.Logger.Info(CWRLocText.Instance.DisableMotionInterpolationMessage.Value);
            DisableMotionInterpolationMethod?.Invoke(null, null);
        }
    }
}

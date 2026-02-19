using CalamityOverhaul.OtherMods.HighFPSSupport;
using InnoVault.GameSystem;
using Terraria.ModLoader.IO;
using SettingToggle = CalamityOverhaul.Content.UIs.OverhaulSettings.OverhaulSettingsUI.SettingToggle;

namespace CalamityOverhaul.Content.UIs.OverhaulSettings
{
    /// <summary>
    /// 兼容性设置的持久化存储
    /// </summary>
    internal class CompatibilitySettingsSave : SaveMod
    {
        /// <summary>
        /// 是否自动禁用 HighFPSSupport 的运动插值功能（默认开启）
        /// </summary>
        public static bool AutoDisableHighFPSMotionInterpolation = true;

        public override void SetStaticDefaults() {
            if (!HasSave) {
                DoSave<CompatibilitySettingsSave>();
            }
            DoLoad<CompatibilitySettingsSave>();
        }

        public override void SaveData(TagCompound tag) {
            tag["AutoDisableHighFPSMotionInterpolation"] = AutoDisableHighFPSMotionInterpolation;
        }

        public override void LoadData(TagCompound tag) {
            if (tag.TryGet("AutoDisableHighFPSMotionInterpolation", out bool value)) {
                AutoDisableHighFPSMotionInterpolation = value;
            }
            else {
                AutoDisableHighFPSMotionInterpolation = true;
            }
        }

        public static void Save() => DoSave<CompatibilitySettingsSave>();
    }

    /// <summary>
    /// 兼容性设置分类：管理与其他模组的兼容性选项
    /// </summary>
    internal class CompatibilitySettingsCategory : SettingsCategory
    {
        public override string Title => OverhaulSettingsUI.CompatibilitySettingsText?.Value ?? "兼容性设置";

        public override void Initialize() {
            AddToggle("AutoDisableHighFPSMotionInterpolation",
                () => CompatibilitySettingsSave.AutoDisableHighFPSMotionInterpolation,
                v => CompatibilitySettingsSave.AutoDisableHighFPSMotionInterpolation = v,
                false);

            ShowFooter = true;
            FooterHint = OverhaulSettingsUI.CompatibilityFooterHintText?.Value ?? "";
        }

        public override string GetLabel(SettingToggle toggle) {
            if (toggle.ConfigPropertyName == "AutoDisableHighFPSMotionInterpolation") {
                return OverhaulSettingsUI.CompatHighFPSLabelText?.Value ?? "自动关闭 HighFPSSupport 运动插值";
            }
            return toggle.ConfigPropertyName;
        }

        public override string GetTooltip(SettingToggle toggle) {
            if (toggle.ConfigPropertyName == "AutoDisableHighFPSMotionInterpolation") {
                string tip = OverhaulSettingsUI.CompatHighFPSTooltipText?.Value ?? "";
                if (!HighFPSRef.Has) {
                    string notInstalled = OverhaulSettingsUI.CompatHighFPSNotInstalledText?.Value
                        ?? "[c/888888:（当前未安装 HighFPSSupport 模组，此选项暂无效果）]";
                    tip += (string.IsNullOrEmpty(tip) ? "" : "\n") + notInstalled;
                }
                return tip;
            }
            return "";
        }

        public override void OnToggleChanged(SettingToggle toggle, bool newValue) {
            CompatibilitySettingsSave.Save();
        }
    }
}

using CalamityOverhaul.Common;
using CalamityOverhaul.OtherMods.Calamity;
using System;
using System.Collections.Generic;
using Terraria.Localization;
using SettingToggle = CalamityOverhaul.Content.UIs.OverhaulSettings.OverhaulSettingsUI.SettingToggle;

namespace CalamityOverhaul.Content.UIs.OverhaulSettings
{
    /// <summary>
    /// 模组兼容：以弱引用补丁形式修复其他模组已知问题的开关合集。
    /// 开关全部为客户端配置且运行时读取，切换后即时生效无需重载；
    /// 悬浮提示附带补丁挂载状态，目标模组版本变动导致反射失败时可一眼看出
    /// </summary>
    internal class ModCompatSettingsCategory : SettingsCategory
    {
        public override string Title => OverhaulSettingsUI.ModCompatSettingsText?.Value ?? "模组兼容";

        //属性名 → 补丁是否已挂载，用于提示状态行
        private readonly Dictionary<string, Func<bool>> patchStatusByProperty = [];

        public override void Initialize() {
            patchStatusByProperty.Clear();
            var config = CWRClientConfig.Instance;
            if (config == null) {
                return;
            }

            if (CWRMod.Instance.calamity != null) {
                AddPatchToggle(nameof(config.CalamityRarityTextFix),
                    () => config.CalamityRarityTextFix, v => config.CalamityRarityTextFix = v,
                    CalamityPatchBase.IsApplied<CalamityTextEffectFix>);
                AddPatchToggle(nameof(config.CalamityHolyBurnOrbFix),
                    () => config.CalamityHolyBurnOrbFix, v => config.CalamityHolyBurnOrbFix = v,
                    CalamityPatchBase.IsApplied<CalamityHolyBurnOrbFix>);
            }

            ShowFooter = true;
            if (Toggles.Count == 0) {
                FooterHint = OverhaulSettingsUI.ModCompatEmptyHintText?.Value ?? "";
                return;
            }

            FooterHint = OverhaulSettingsUI.ModCompatFooterHintText?.Value ?? "";
            ActionButtons.Add(new ActionButton {
                Label = () => OverhaulSettingsUI.ResetDefaultText?.Value ?? "重置为默认",
                OnClick = ResetAllToDefault
            });
        }

        private void AddPatchToggle(string propertyName, Func<bool> getter, Action<bool> setter, Func<bool> applied) {
            AddToggle(propertyName, getter, setter, false);
            patchStatusByProperty[propertyName] = applied;
        }

        private void ResetAllToDefault() {
            foreach (var toggle in Toggles) {
                toggle.Setter(true);
            }
            SaveConfig();
        }

        public override string GetLabel(SettingToggle toggle) {
            string key = $"Mods.CalamityOverhaul.Configs.{nameof(CWRClientConfig)}.{toggle.ConfigPropertyName}.Label";
            string value = Language.GetTextValue(key);
            return value == key ? toggle.ConfigPropertyName : value;
        }

        public override string GetTooltip(SettingToggle toggle) {
            string key = $"Mods.CalamityOverhaul.Configs.{nameof(CWRClientConfig)}.{toggle.ConfigPropertyName}.Tooltip";
            string value = Language.GetTextValue(key);
            string tip = value == key ? "" : value;

            if (patchStatusByProperty.TryGetValue(toggle.ConfigPropertyName, out Func<bool> applied)) {
                string status = applied()
                    ? $"[c/7FD87F:{OverhaulSettingsUI.PatchStatusActiveText?.Value ?? "补丁状态：已挂载"}]"
                    : $"[c/FF7F7F:{OverhaulSettingsUI.PatchStatusInactiveText?.Value ?? "补丁状态：未挂载"}]";
                tip = string.IsNullOrEmpty(tip) ? status : tip + "\n" + status;
            }
            return tip;
        }

        public override void OnToggleChanged(SettingToggle toggle, bool newValue) {
            SaveConfig();
        }

        private static void SaveConfig() {
            ContentSettingsCategory.SaveConfigStatic();
        }
    }
}

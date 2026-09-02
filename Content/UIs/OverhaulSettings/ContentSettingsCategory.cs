using CalamityOverhaul.Common;
using System.Collections.Generic;
using System.Reflection;
using Terraria.Localization;
using Terraria.ModLoader.Config;
using SettingToggle = CalamityOverhaul.Content.UIs.OverhaulSettings.OverhaulSettingsUI.SettingToggle;

namespace CalamityOverhaul.Content.UIs.OverhaulSettings
{
    /// <summary>CWRServerConfig 与 CWRClientConfig 的布尔项混合展示</summary>
    internal class ContentSettingsCategory : SettingsCategory
    {
        public override string Title => OverhaulSettingsUI.ContentSettingsText?.Value ?? "内容设置";

        private bool needsReload;
        public bool NeedsReload => needsReload;

        //纯本地视觉偏好，本地化键指向 CWRClientConfig 而非 CWRServerConfig
        private static readonly HashSet<string> ClientConfigProperties = [
            nameof(CWRClientConfig.ScreenVibration),
            nameof(CWRClientConfig.DomainConciseDisplay),
            nameof(CWRClientConfig.LensEasing),
            nameof(CWRClientConfig.RarityTextEffects),
        ];

        private static string ConfigClassNameFor(string propertyName)
            => ClientConfigProperties.Contains(propertyName) ? nameof(CWRClientConfig) : nameof(CWRServerConfig);

        private static MethodInfo _configManagerSave;

        public static void LoadReflection() {
            _configManagerSave = typeof(ConfigManager)
                .GetMethod("Save", BindingFlags.Static | BindingFlags.NonPublic);
        }

        public static void UnloadReflection() {
            _configManagerSave = null;
        }

        public override void Initialize() {
            if (CWRClientConfig.Instance != null) {
                var clientConfig = CWRClientConfig.Instance;
                //CWRWeapon(纯本地偏好)
                AddToggle(nameof(clientConfig.ScreenVibration), () => clientConfig.ScreenVibration, v => clientConfig.ScreenVibration = v, false);
                AddToggle(nameof(clientConfig.DomainConciseDisplay), () => clientConfig.DomainConciseDisplay, v => clientConfig.DomainConciseDisplay = v, false);
                AddToggle(nameof(clientConfig.LensEasing), () => clientConfig.LensEasing, v => clientConfig.LensEasing = v, false);
                AddToggle(nameof(clientConfig.RarityTextEffects), () => clientConfig.RarityTextEffects, v => clientConfig.RarityTextEffects = v, false);
            }

            ActionButtons.Add(new ActionButton {
                Label = () => OverhaulSettingsUI.ResetDefaultText?.Value ?? "重置为默认",
                OnClick = ResetAllToDefault
            });
        }

        private void ResetAllToDefault() {
            var clientConfig = CWRClientConfig.Instance;
            if (clientConfig != null) {
                //CWRWeapon
                clientConfig.ScreenVibration = true;
                clientConfig.DomainConciseDisplay = false;
                clientConfig.LensEasing = true;
                clientConfig.RarityTextEffects = true;
            }

            SaveConfig();

            bool hasReloadToggle = false;
            foreach (var toggle in Toggles) {
                if (toggle.RequiresReload) {
                    hasReloadToggle = true;
                    break;
                }
            }
            if (hasReloadToggle) {
                needsReload = true;
                ShowFooter = true;
                FooterHint = OverhaulSettingsUI.ReloadHintText?.Value ?? "";
            }
        }

        public override string GetLabel(SettingToggle toggle) {
            string key = $"Mods.CalamityOverhaul.Configs.{ConfigClassNameFor(toggle.ConfigPropertyName)}.{toggle.ConfigPropertyName}.Label";
            string value = Language.GetTextValue(key);
            string label = value == key ? toggle.ConfigPropertyName : value;
            if (toggle.RequiresReload) {
                label = "[c/FF6666:*] " + label;
            }
            return label;
        }

        public override string GetTooltip(SettingToggle toggle) {
            string key = $"Mods.CalamityOverhaul.Configs.{ConfigClassNameFor(toggle.ConfigPropertyName)}.{toggle.ConfigPropertyName}.Tooltip";
            string value = Language.GetTextValue(key);
            return value == key ? "" : value;
        }

        public override void OnToggleChanged(SettingToggle toggle, bool newValue) {
            SaveConfig();
            if (toggle.RequiresReload) {
                needsReload = true;
                ShowFooter = true;
                FooterHint = OverhaulSettingsUI.ReloadHintText?.Value ?? "";
            }
        }

        private static void SaveConfig() {
            SaveConfigStatic();
        }

        internal static void SaveConfigStatic() {
            if (CWRServerConfig.Instance != null) {
                CWRServerConfig.Instance.OnChanged();
                _configManagerSave?.Invoke(null, [CWRServerConfig.Instance]);
            }
            if (CWRClientConfig.Instance != null) {
                _configManagerSave?.Invoke(null, [CWRClientConfig.Instance]);
            }
        }
    }
}

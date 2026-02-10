using CalamityOverhaul.Common;
using System.Reflection;
using Terraria.Localization;
using Terraria.ModLoader.Config;
using SettingToggle = CalamityOverhaul.Content.UIs.OverhaulSettings.OverhaulSettingsUI.SettingToggle;

namespace CalamityOverhaul.Content.UIs.OverhaulSettings
{
    /// <summary>
    /// 内容设置分类：管理 CWRServerConfig 中的布尔配置项
    /// </summary>
    internal class ContentSettingsCategory : SettingsCategory
    {
        public override string Title => OverhaulSettingsUI.ContentSettingsText?.Value ?? "内容设置";

        private bool needsReload;
        public bool NeedsReload => needsReload;

        private static MethodInfo _configManagerSave;

        public static void LoadReflection() {
            _configManagerSave = typeof(ConfigManager)
                .GetMethod("Save", BindingFlags.Static | BindingFlags.NonPublic);
        }

        public static void UnloadReflection() {
            _configManagerSave = null;
        }

        public override void Initialize() {
            if (CWRServerConfig.Instance == null) return;

            var config = CWRServerConfig.Instance;

            //CWRSystem组(需要重载)
            AddToggle("QuestLog", () => config.QuestLog, v => config.QuestLog = v, true);
            AddToggle("BiologyOverhaul", () => config.BiologyOverhaul, v => config.BiologyOverhaul = v, true);

            //CWRWeapon组
            AddToggle("WeaponHandheldDisplay", () => config.WeaponHandheldDisplay, v => config.WeaponHandheldDisplay = v, false);
            AddToggle("EnableSwordLight", () => config.EnableSwordLight, v => config.EnableSwordLight = v, false);
            AddToggle("ActivateGunRecoil", () => config.ActivateGunRecoil, v => config.ActivateGunRecoil = v, false);
            AddToggle("MagazineSystem", () => config.MagazineSystem, v => config.MagazineSystem = v, false);
            AddToggle("EnableCasingsEntity", () => config.EnableCasingsEntity, v => config.EnableCasingsEntity = v, false);
            AddToggle("BowArrowDraw", () => config.BowArrowDraw, v => config.BowArrowDraw = v, false);
            AddToggle("ShotgunFireForcedReloadInterruption", () => config.ShotgunFireForcedReloadInterruption, v => config.ShotgunFireForcedReloadInterruption = v, false);
            AddToggle("WeaponLazyRotationAngle", () => config.WeaponLazyRotationAngle, v => config.WeaponLazyRotationAngle = v, false);
            AddToggle("ScreenVibration", () => config.ScreenVibration, v => config.ScreenVibration = v, false);
            AddToggle("MurasamaSpaceFragmentationBool", () => config.MurasamaSpaceFragmentationBool, v => config.MurasamaSpaceFragmentationBool = v, false);
            AddToggle("HalibutDomainConciseDisplay", () => config.HalibutDomainConciseDisplay, v => config.HalibutDomainConciseDisplay = v, false);
            AddToggle("LensEasing", () => config.LensEasing, v => config.LensEasing = v, false);

            //CWRUI组
            AddToggle("ShowReloadingProgressUI", () => config.ShowReloadingProgressUI, v => config.ShowReloadingProgressUI = v, false);

            ActionButtons.Add(new ActionButton {
                Label = () => OverhaulSettingsUI.ResetDefaultText?.Value ?? "重置为默认",
                OnClick = ResetAllToDefault
            });
        }

        private void ResetAllToDefault() {
            var config = CWRServerConfig.Instance;
            if (config == null) return;

            //CWRSystem组
            config.QuestLog = true;
            config.BiologyOverhaul = true;

            //CWRWeapon组
            config.WeaponHandheldDisplay = true;
            config.EnableSwordLight = true;
            config.ActivateGunRecoil = false;
            config.MagazineSystem = true;
            config.EnableCasingsEntity = true;
            config.BowArrowDraw = true;
            config.ShotgunFireForcedReloadInterruption = false;
            config.WeaponLazyRotationAngle = false;
            config.ScreenVibration = true;
            config.MurasamaSpaceFragmentationBool = true;
            config.HalibutDomainConciseDisplay = false;
            config.LensEasing = true;

            //CWRUI组
            config.ShowReloadingProgressUI = false;

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
            string key = $"Mods.CalamityOverhaul.Configs.CWRServerConfig.{toggle.ConfigPropertyName}.Label";
            string value = Language.GetTextValue(key);
            string label = value == key ? toggle.ConfigPropertyName : value;
            if (toggle.RequiresReload) {
                label = "[c/FF6666:*] " + label;
            }
            return label;
        }

        public override string GetTooltip(SettingToggle toggle) {
            string key = $"Mods.CalamityOverhaul.Configs.CWRServerConfig.{toggle.ConfigPropertyName}.Tooltip";
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
            if (CWRServerConfig.Instance == null) return;
            CWRServerConfig.Instance.OnChanged();
            _configManagerSave?.Invoke(null, [CWRServerConfig.Instance]);
        }
    }
}

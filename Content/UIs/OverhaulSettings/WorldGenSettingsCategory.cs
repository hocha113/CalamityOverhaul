using CalamityOverhaul.Common;
using Terraria.Localization;
using SettingToggle = CalamityOverhaul.Content.UIs.OverhaulSettings.OverhaulSettingsUI.SettingToggle;

namespace CalamityOverhaul.Content.UIs.OverhaulSettings
{
    /// <summary>
    /// 世界生成设置分类：管理 CWRServerConfig 中与世界生成结构相关的布尔配置项，
    /// 允许玩家决定世界生成时是否添加这些结构
    /// </summary>
    internal class WorldGenSettingsCategory : SettingsCategory
    {
        public override string Title => OverhaulSettingsUI.WorldGenSettingsText?.Value ?? "世界生成设置";

        public override void Initialize() {
            if (CWRServerConfig.Instance == null) return;

            var config = CWRServerConfig.Instance;

            AddToggle("GenWindGrivenGenerator", () => config.GenWindGrivenGenerator, v => config.GenWindGrivenGenerator = v, false);
            AddToggle("GenWGGCollector", () => config.GenWGGCollector, v => config.GenWGGCollector = v, false);
            AddToggle("GenJunkmanBase", () => config.GenJunkmanBase, v => config.GenJunkmanBase = v, false);
            AddToggle("GenRocketHut", () => config.GenRocketHut, v => config.GenRocketHut = v, false);
            AddToggle("GenSylvanOutpost", () => config.GenSylvanOutpost, v => config.GenSylvanOutpost = v, false);

            ShowFooter = true;
            FooterHint = OverhaulSettingsUI.WorldGenFooterHintText?.Value ?? "";
        }

        public override string GetLabel(SettingToggle toggle) {
            string key = $"Mods.CalamityOverhaul.Configs.CWRServerConfig.{toggle.ConfigPropertyName}.Label";
            string value = Language.GetTextValue(key);
            return value == key ? toggle.ConfigPropertyName : value;
        }

        public override string GetTooltip(SettingToggle toggle) {
            string key = $"Mods.CalamityOverhaul.Configs.CWRServerConfig.{toggle.ConfigPropertyName}.Tooltip";
            string value = Language.GetTextValue(key);
            return value == key ? "" : value;
        }

        public override void OnToggleChanged(SettingToggle toggle, bool newValue) {
            SaveConfig();
        }

        private static void SaveConfig() {
            if (CWRServerConfig.Instance == null) return;
            CWRServerConfig.Instance.OnChanged();
            ContentSettingsCategory.SaveConfigStatic();
        }
    }
}

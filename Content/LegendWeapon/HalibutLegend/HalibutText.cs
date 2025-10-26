using CalamityOverhaul.Common;
using System;
using System.Collections.Generic;
using System.Reflection;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend
{
    internal class HalibutText : ModType, ILocalizedModType
    {
        public string LocalizationCategory => "Legend";
        public static HalibutText Instance => ModContent.GetInstance<HalibutText>();
        protected override void Register() { }
        #region 字段内容
        public LocalizedText FishByStudied { get; private set; }
        public LocalizedText FishOnStudied { get; private set; }
        public LocalizedText FishByPuzzle { get; private set; }
        #endregion
        #region Utils
        public static string GetTextKey(string key) => $"Mods.CalamityOverhaul.Legend.HalibutText.{key}";
        public static string GetTextValue(string key) => Language.GetTextValue($"Mods.CalamityOverhaul.Legend.HalibutText.{key}");
        public static LocalizedText GetText(string key) => Language.GetText($"Mods.CalamityOverhaul.Legend.HalibutText.{key}");
        #endregion
        public override void Load() {
            //使用反射进行属性的自动加载
            PropertyInfo[] properties = GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (PropertyInfo property in properties) {
                if (property.PropertyType == typeof(LocalizedText)) {
                    property.SetValue(this, this.GetLocalization(property.Name));
                }
            }
        }

        public static void SetTooltip(Item item, ref List<TooltipLine> tooltips) {
            int index = HalibutData.GetLevel(item);
            string newContent = index >= 0 && index <= 14 ? CWRLocText.GetTextValue($"Halibut_TextDictionary_Content_{index}") : "ERROR";
            string num = (index + 1).ToString();
            if (index == 14) {
                num = CWRLocText.GetTextValue("Murasama_Text_Lang_End");
            }

            string text = LegendData.GetLevelTrialPreText(item.CWR(), "Murasama_Text_Lang_0", num);

            tooltips.ReplacePlaceholder("[Lang4]", text, "");
            tooltips.ReplacePlaceholder("legend_Text", CWRLocText.GetTextValue("Halibut_No_legend_Content_3"), "");
            Color newColor = Color.Lerp(Color.IndianRed, Color.White, 0.5f + (float)Math.Sin(Main.GlobalTimeWrappedHourly) * 0.5f);
            tooltips.ReplacePlaceholder("[Text]", VaultUtils.FormatColorTextMultiLine(newContent, newColor), "");
        }
    }
}

using CalamityOverhaul.Common;
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
            //试炼文本已迁移至委托系统(HalibutQuestLine)，此处仅保留传奇状态文本
            string keyDisplay = CWRKeySystem.QuestManager_Key?.GetAssignedKeys() is { Count: > 0 } k ? k[0] : CWRLocText.Instance.Notbound.Value;
            tooltips.ReplacePlaceholder("legend_Text", CWRLocText.GetTextValue("Legend_QuestManager_Hint").Replace("{KEY}", keyDisplay), "");
            int index = item.CWR()?.LegendData?.TargetLevel ?? 0;
            string num = (index + 1).ToString();
            if (index == 14) {
                num = CWRLocText.GetTextValue("Murasama_Text_Lang_End");
            }
            string text = LegendData.GetLevelTrialPreText(item.CWR(), "Murasama_Text_Lang_0", num);
            tooltips.ReplacePlaceholder("[Lang4]", text, "");
        }
    }
}

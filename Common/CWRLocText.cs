using System;
using System.Reflection;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Common
{
    internal class CWRLocText : ModType, ILocalizedModType
    {
        public string LocalizationCategory => "TextContent";
        protected override void Register() { }
        #region Utils
        public static string GetTextValue(string key) => Language.GetTextValue($"Mods.CalamityOverhaul.TextContent.CWRLocText.{key}");
        public static LocalizedText GetText(string key) => Language.GetText($"Mods.CalamityOverhaul.TextContent.CWRLocText.{key}");
        #endregion
        #region 字段内容
        public LocalizedText Item_LegendOnMouseLang { get; private set; }
        public LocalizedText BloodAltar_Text1 { get; private set; }
        public LocalizedText BloodAltar_Text2 { get; private set; }
        public LocalizedText BloodAltar_Text3 { get; private set; }
        public LocalizedText VoidDamageNameText { get; private set; }
        public LocalizedText Drop_Hell_RuleText { get; private set; }
        public LocalizedText Drop_GlodDragonDrop_RuleText { get; private set; }
        public LocalizedText Murasama_TextDictionary_Content { get; private set; }
        public LocalizedText RemakeItem_Remind_TextContent { get; private set; }
        public LocalizedText StarMyriadChanges_TextContent { get; private set; }
        public LocalizedText Destruct_TextContent { get; private set; }
        public LocalizedText CWRRecipes_ApostolicRelics { get; private set; }
        public LocalizedText CWRRecipes_GodEaterWeapon { get; private set; }
        public LocalizedText CWRRecipes_FishGroup { get; private set; }
        public LocalizedText Murasama_Text0 { get; private set; }
        public LocalizedText Murasama_Text1 { get; private set; }
        public LocalizedText Murasama_Text2 { get; private set; }
        public LocalizedText Murasama_Text3 { get; private set; }
        public LocalizedText MouseTextContactPanel_TextContent { get; private set; }
        public LocalizedText CWRItem_IsInfiniteItem_TextContent { get; private set; }
        public LocalizedText CWRItem_IsRemakeItem_TextContent { get; private set; }
        public LocalizedText OnEnterWorld_TextContent { get; private set; }
        public LocalizedText CaseEjection_TextContent { get; private set; }
        public LocalizedText SupertableUI_Text1 { get; private set; }
        public LocalizedText SupertableUI_Text2 { get; private set; }
        public LocalizedText SupertableUI_Text3 { get; private set; }
        public LocalizedText OverhaulTheBibleUI_Text { get; private set; }
        public LocalizedText Wap_Minishark_Text { get; private set; }
        public LocalizedText Wap_Megashark_Text { get; private set; }
        public LocalizedText Wap_HandGun_Text { get; private set; }
        public LocalizedText Wap_Flintlockpistol_Text { get; private set; }
        public LocalizedText Wap_Musket_Text { get; private set; }
        public LocalizedText Wap_TheUndertaker_Text { get; private set; }
        public LocalizedText Wap_RedRyder_Text { get; private set; }
        public LocalizedText Wap_FlareGun_Text { get; private set; }
        public LocalizedText Wap_PhoenixBlaster_Text { get; private set; }
        public LocalizedText Wap_Sandgun_Text { get; private set; }
        public LocalizedText Wap_StarCannon_Text { get; private set; }
        public LocalizedText Wap_SuperStarCannon_Text { get; private set; }
        public LocalizedText Wap_ZapinatorGray_Text { get; private set; }
        public LocalizedText Wap_ZapinatorOrange_Text { get; private set; }
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
    }
}

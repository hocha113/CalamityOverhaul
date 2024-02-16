using System;
using System.Reflection;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Common
{
    internal class CWRLocalizationText : ModType, ILocalizedModType
    {
        public string LocalizationCategory => "TextContent";

        protected override void Register() { }

        public static string GetTextValue(string key) => Language.GetTextValue($"Mods.CalamityOverhaul.TextContent.CWRLocalizationText.{key}");

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

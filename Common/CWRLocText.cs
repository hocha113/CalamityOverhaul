using System.Reflection;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Common
{
    internal class CWRLocText : ModType, ILocalizedModType
    {
        public string LocalizationCategory => "TextContent";
        public static CWRLocText Instance { get; private set; }
        //不要被吓到，这些只是必须的
        #region 字段内容
        public LocalizedText IndustrializationGenMessage { get; private set; }
        public LocalizedText InternalStoredEnergy { get; private set; }
        public LocalizedText KreloadTimeAddText { get; private set; }
        public LocalizedText KreloadTimeLessenText { get; private set; }
        public LocalizedText DeathModeItem { get; private set; }
        public LocalizedText DontUseMagicConch { get; private set; }
        public LocalizedText OnlyZenith { get; private set; }
        public LocalizedText Event_TungstenRiot_Name { get; private set; }
        public LocalizedText Event_TungstenRiot_Text_1 { get; private set; }
        public LocalizedText Event_TungstenRiot_Text_2 { get; private set; }
        public LocalizedText Item_LegendOnMouseLang { get; private set; }
        public LocalizedText BloodAltar_Text1 { get; private set; }
        public LocalizedText BloodAltar_Text2 { get; private set; }
        public LocalizedText BloodAltar_Text3 { get; private set; }
        public LocalizedText VoidDamageNameText { get; private set; }
        public LocalizedText Drop_Hell_RuleText { get; private set; }
        public LocalizedText Drop_GlodDragonDrop_RuleText { get; private set; }
        public LocalizedText Murasama_Text_Lang_0 { get; private set; }
        public LocalizedText Murasama_Text_Lang_End { get; private set; }
        public LocalizedText Murasama_TextDictionary_Content_0 { get; private set; }
        public LocalizedText Murasama_TextDictionary_Content_1 { get; private set; }
        public LocalizedText Murasama_TextDictionary_Content_2 { get; private set; }
        public LocalizedText Murasama_TextDictionary_Content_3 { get; private set; }
        public LocalizedText Murasama_TextDictionary_Content_4 { get; private set; }
        public LocalizedText Murasama_TextDictionary_Content_5 { get; private set; }
        public LocalizedText Murasama_TextDictionary_Content_6 { get; private set; }
        public LocalizedText Murasama_TextDictionary_Content_7 { get; private set; }
        public LocalizedText Murasama_TextDictionary_Content_8 { get; private set; }
        public LocalizedText Murasama_TextDictionary_Content_9 { get; private set; }
        public LocalizedText Murasama_TextDictionary_Content_10 { get; private set; }
        public LocalizedText Murasama_TextDictionary_Content_11 { get; private set; }
        public LocalizedText Murasama_TextDictionary_Content_12 { get; private set; }
        public LocalizedText Murasama_TextDictionary_Content_13 { get; private set; }
        public LocalizedText Murasama_TextDictionary_Content_14 { get; private set; }
        public LocalizedText Murasama_No_legend_Content_1 { get; private set; }
        public LocalizedText Murasama_No_legend_Content_2 { get; private set; }
        public LocalizedText Murasama_No_legend_Content_3 { get; private set; }
        public LocalizedText Murasama_No_legend_Content_4 { get; private set; }
        public LocalizedText SHPC_No_legend_Content_1 { get; private set; }
        public LocalizedText SHPC_No_legend_Content_2 { get; private set; }
        public LocalizedText SHPC_No_legend_Content_3 { get; private set; }
        public LocalizedText SHPC_No_legend_Content_4 { get; private set; }
        public LocalizedText SHPC_TextDictionary_Content_0 { get; private set; }
        public LocalizedText SHPC_TextDictionary_Content_1 { get; private set; }
        public LocalizedText SHPC_TextDictionary_Content_2 { get; private set; }
        public LocalizedText SHPC_TextDictionary_Content_3 { get; private set; }
        public LocalizedText SHPC_TextDictionary_Content_4 { get; private set; }
        public LocalizedText SHPC_TextDictionary_Content_5 { get; private set; }
        public LocalizedText SHPC_TextDictionary_Content_6 { get; private set; }
        public LocalizedText SHPC_TextDictionary_Content_7 { get; private set; }
        public LocalizedText SHPC_TextDictionary_Content_8 { get; private set; }
        public LocalizedText SHPC_TextDictionary_Content_9 { get; private set; }
        public LocalizedText SHPC_TextDictionary_Content_10 { get; private set; }
        public LocalizedText SHPC_TextDictionary_Content_11 { get; private set; }
        public LocalizedText SHPC_TextDictionary_Content_12 { get; private set; }
        public LocalizedText SHPC_TextDictionary_Content_13 { get; private set; }
        public LocalizedText SHPC_TextDictionary_Content_14 { get; private set; }
        public LocalizedText Halibut_No_legend_Content_1 { get; private set; }
        public LocalizedText Halibut_No_legend_Content_2 { get; private set; }
        public LocalizedText Halibut_No_legend_Content_3 { get; private set; }
        public LocalizedText Halibut_No_legend_Content_4 { get; private set; }
        public LocalizedText Halibut_TextDictionary_Content_0 { get; private set; }
        public LocalizedText Halibut_TextDictionary_Content_1 { get; private set; }
        public LocalizedText Halibut_TextDictionary_Content_2 { get; private set; }
        public LocalizedText Halibut_TextDictionary_Content_3 { get; private set; }
        public LocalizedText Halibut_TextDictionary_Content_4 { get; private set; }
        public LocalizedText Halibut_TextDictionary_Content_5 { get; private set; }
        public LocalizedText Halibut_TextDictionary_Content_6 { get; private set; }
        public LocalizedText Halibut_TextDictionary_Content_7 { get; private set; }
        public LocalizedText Halibut_TextDictionary_Content_8 { get; private set; }
        public LocalizedText Halibut_TextDictionary_Content_9 { get; private set; }
        public LocalizedText Halibut_TextDictionary_Content_10 { get; private set; }
        public LocalizedText Halibut_TextDictionary_Content_11 { get; private set; }
        public LocalizedText Halibut_TextDictionary_Content_12 { get; private set; }
        public LocalizedText Halibut_TextDictionary_Content_13 { get; private set; }
        public LocalizedText Halibut_TextDictionary_Content_14 { get; private set; }
        public LocalizedText CWRRecipes_ApostolicRelics { get; private set; }
        public LocalizedText CWRRecipes_GodEaterWeapon { get; private set; }
        public LocalizedText CWRRecipes_FishGroup { get; private set; }
        public LocalizedText LoadItemRecipe_Condition_Text1 { get; private set; }
        public LocalizedText Murasama_Text0 { get; private set; }
        public LocalizedText Murasama_Text1 { get; private set; }
        public LocalizedText Murasama_Text2 { get; private set; }
        public LocalizedText Murasama_Text3 { get; private set; }
        public LocalizedText MouseTextContactPanel_TextContent { get; private set; }
        public LocalizedText CWRItem_IsRemakeItem_TextContent { get; private set; }
        public LocalizedText OnEnterWorld_TextContent { get; private set; }
        public LocalizedText OnEnterWorld_TextContent2 { get; private set; }
        public LocalizedText CaseEjection_TextContent { get; private set; }
        public LocalizedText CaseEjection_TextContent_Coin { get; private set; }
        public LocalizedText CaseEjection_TextContent_Arrow { get; private set; }
        public LocalizedText SupertableUI_Text1 { get; private set; }
        public LocalizedText SupertableUI_Text2 { get; private set; }
        public LocalizedText SupertableUI_Text3 { get; private set; }
        public LocalizedText SupertableUI_Text4 { get; private set; }
        public LocalizedText SupertableUI_Text5 { get; private set; }
        public LocalizedText OverhaulTheBibleUI_Text { get; private set; }
        public LocalizedText OverhaulTheBibleUI_Text1 { get; private set; }
        public LocalizedText OverhaulTheBibleUI_Text2 { get; private set; }
        public LocalizedText OverhaulTheBibleUI_Text3 { get; private set; }
        public LocalizedText OverhaulTheBibleUI_Text4 { get; private set; }
        public LocalizedText OverhaulTheBibleUI_Text5 { get; private set; }
        public LocalizedText CartridgeHolderUI_Text1 { get; private set; }
        public LocalizedText CartridgeHolderUI_Text2 { get; private set; }
        public LocalizedText CartridgeHolderUI_Text3 { get; private set; }
        public LocalizedText CartridgeHolderUI_Text4 { get; private set; }
        public LocalizedText CartridgeHolderUI_Text5 { get; private set; }
        public LocalizedText CartridgeHolderUI_Text6 { get; private set; }
        public LocalizedText CartridgeHolderUI_Text7 { get; private set; }
        public LocalizedText ArrowHolderUI_Text0 { get; private set; }
        public LocalizedText ArrowHolderUI_Text1 { get; private set; }
        public LocalizedText CWRGun_KL_Text { get; private set; }
        public LocalizedText CWRGun_Scope_Text { get; private set; }
        public LocalizedText CWRGun_Recoil_Text { get; private set; }
        public LocalizedText CWRGun_MustCA_Text { get; private set; }
        public LocalizedText CWRGun_Recoil_Level_0 { get; private set; }
        public LocalizedText CWRGun_Recoil_Level_1 { get; private set; }
        public LocalizedText CWRGun_Recoil_Level_2 { get; private set; }
        public LocalizedText CWRGun_Recoil_Level_3 { get; private set; }
        public LocalizedText CWRGun_Recoil_Level_4 { get; private set; }
        public LocalizedText CWRGun_Recoil_Level_5 { get; private set; }
        public LocalizedText CWRGun_Recoil_Level_6 { get; private set; }
        public LocalizedText AmmoBox_Text { get; private set; }
        public LocalizedText AmmoBox_Text2 { get; private set; }
        public LocalizedText AmmoBox_Text3 { get; private set; }
        public LocalizedText HellfireExplosion_DeadLang_Text { get; private set; }
        public LocalizedText SoulfireExplosion_DeadLang_Text { get; private set; }
        public LocalizedText SupMUI_OneClick_Text1 { get; private set; }
        public LocalizedText SupMUI_OneClick_Text2 { get; private set; }
        public LocalizedText TemporaryVersion_Text { get; private set; }
        public LocalizedText MS_Config_Text { get; private set; }
        public LocalizedText IconUI_Text0 { get; private set; }
        public LocalizedText IconUI_Text1 { get; private set; }
        public LocalizedText IconUI_Text2 { get; private set; }
        public LocalizedText IconUI_Text3 { get; private set; }
        public LocalizedText IconUI_Text4 { get; private set; }
        public LocalizedText IconUI_Text5 { get; private set; }
        public LocalizedText IconUI_Text6 { get; private set; }
        public LocalizedText IconUI_Text7 { get; private set; }
        public LocalizedText IconUI_Text8 { get; private set; }
        public LocalizedText SPU_Text0 { get; private set; }
        public LocalizedText SPU_Text1 { get; private set; }
        public LocalizedText SPU_Text2 { get; private set; }
        public LocalizedText Spazmatism_Text1 { get; private set; }
        public LocalizedText Spazmatism_Text2 { get; private set; }
        public LocalizedText Spazmatism_Text3 { get; private set; }
        public LocalizedText Spazmatism_Text4 { get; private set; }
        public LocalizedText Spazmatism_Text5 { get; private set; }
        public LocalizedText Spazmatism_Text6 { get; private set; }
        public LocalizedText Spazmatism_Text7 { get; private set; }
        public LocalizedText Spazmatism_Text8 { get; private set; }
        public LocalizedText Error_1 { get; private set; }
        public LocalizedText Error_2 { get; private set; }
        public LocalizedText Config_1 { get; private set; }
        public LocalizedText Config_2 { get; private set; }
        public LocalizedText SkeletronPrime_Text { get; private set; }
        public LocalizedText MachineRebellion_SpawnInfo { get; private set; }
        public LocalizedText MachineRebellion_DespawnMessage { get; private set; }
        public LocalizedText MachineRebellion_DisplayName { get; private set; }
        #endregion
        protected override void Register() => Instance = this;
        #region Utils
        public static string GetTextKey(string key) => $"Mods.CalamityOverhaul.TextContent.CWRLocText.{key}";
        public static string GetTextValue(string key) => Language.GetTextValue($"Mods.CalamityOverhaul.TextContent.CWRLocText.{key}");
        public static LocalizedText GetText(string key) => Language.GetText($"Mods.CalamityOverhaul.TextContent.CWRLocText.{key}");
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

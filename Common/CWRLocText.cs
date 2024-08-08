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
        //不要被吓到，这些只是必须的
        #region 字段内容
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
        public LocalizedText Murasama_TextDictionary_Content { get; private set; }
        public LocalizedText Murasama_Text_Lang_0 { get; private set; }
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
        public LocalizedText RemakeItem_Remind_TextContent { get; private set; }
        public LocalizedText StarMyriadChanges_TextContent { get; private set; }
        public LocalizedText Destruct_TextContent { get; private set; }
        public LocalizedText CWRRecipes_ApostolicRelics { get; private set; }
        public LocalizedText CWRRecipes_GodEaterWeapon { get; private set; }
        public LocalizedText CWRRecipes_FishGroup { get; private set; }
        public LocalizedText LoadItemRecipe_Condition_Text1 { get; private set; }
        public LocalizedText Murasama_Text0 { get; private set; }
        public LocalizedText Murasama_Text1 { get; private set; }
        public LocalizedText Murasama_Text2 { get; private set; }
        public LocalizedText Murasama_Text3 { get; private set; }
        public LocalizedText MouseTextContactPanel_TextContent { get; private set; }
        public LocalizedText CWRItem_IsInfiniteItem_TextContent { get; private set; }
        public LocalizedText CWRItem_IsRemakeItem_TextContent { get; private set; }
        public LocalizedText OnEnterWorld_TextContent { get; private set; }
        public LocalizedText OnEnterWorld_TextContent2 { get; private set; }
        public LocalizedText CaseEjection_TextContent { get; private set; }
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
        public LocalizedText Error_1 { get; private set; }
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
        public LocalizedText Wap_QuadBarrelShotgun_Text { get; private set; }
        public LocalizedText Wap_TacticalShotgun_Text { get; private set; }
        public LocalizedText Wap_Shotgun_Text { get; private set; }
        public LocalizedText Wap_Boomstick_Text { get; private set; }
        public LocalizedText Wap_ClockworkAssaultRifle_Text { get; private set; }
        public LocalizedText Wap_SnowballCannon_Text { get; private set; }
        public LocalizedText Wap_Uzi_Text { get; private set; }
        public LocalizedText Wap_VenusMagnum_Text { get; private set; }
        public LocalizedText Wap_StakeLauncher_Text { get; private set; }
        public LocalizedText Wap_SniperRifle_Text { get; private set; }
        public LocalizedText Wap_NailGun_Text { get; private set; }
        public LocalizedText Wap_DartPistol_Text { get; private set; }
        public LocalizedText Wap_DartRifle_Text { get; private set; }
        public LocalizedText Wap_SDMG_Text { get; private set; }
        public LocalizedText Wap_OnyxBlaster_Text { get; private set; }
        public LocalizedText Wap_LaserRifle_Text { get; private set; }
        public LocalizedText Wap_CobaltRepeater_Text { get; private set; }
        public LocalizedText Wap_PalladiumRepeater_Text { get; private set; }
        public LocalizedText Wap_MythrilRepeater_Text { get; private set; }
        public LocalizedText Wap_OrichalcumRepeater_Text { get; private set; }
        public LocalizedText Wap_AdamantiteRepeater_Text { get; private set; }
        public LocalizedText Wap_TitaniumRepeater_Text { get; private set; }
        public LocalizedText Wap_ChlorophyteShotbow_Text { get; private set; }
        public LocalizedText Wap_HallowedRepeater_Text { get; private set; }
        public LocalizedText Wap_GrenadeLauncher_Text { get; private set; }
        public LocalizedText Wap_RocketLauncher_Text { get; private set; }
        public LocalizedText Wap_ElectrosphereLauncher_Text { get; private set; }
        public LocalizedText Wap_ProximityMineLauncher_Text { get; private set; }
        public LocalizedText Wap_SnowmanCannon_Text { get; private set; }
        public LocalizedText Wap_BubbleGun_Text { get; private set; }
        public LocalizedText Wap_Tsunami_Text { get; private set; }
        public LocalizedText Wap_Revolver_Text { get; private set; }
        public LocalizedText Wap_Gatligator_Text { get; private set; }
        public LocalizedText Wap_Xenopopper_Text { get; private set; }
        public LocalizedText Wap_DaedalusStormbow_Text { get; private set; }
        public LocalizedText Wap_CandyCornRifle_Text { get; private set; }
        public LocalizedText Wap_WoodenBow_Text { get; private set; }
        public LocalizedText Wap_DemonBow_Text { get; private set; }
        public LocalizedText Wap_IronBow_Text { get; private set; }
        public LocalizedText Wap_TungstenBow_Text { get; private set; }
        public LocalizedText Wap_TinBow_Text { get; private set; }
        public LocalizedText Wap_TendonBow_Text { get; private set; }
        public LocalizedText Wap_SilverBow_Text { get; private set; }
        public LocalizedText Wap_ShadowFlameBow_Text { get; private set; }
        public LocalizedText Wap_PlatinumBow_Text { get; private set; }
        public LocalizedText Wap_Marrow_Text { get; private set; }
        public LocalizedText Wap_LeadBow_Text { get; private set; }
        public LocalizedText Wap_IceBow_Text { get; private set; }
        public LocalizedText Wap_GoldBow_Text { get; private set; }
        public LocalizedText Wap_CopperBow_Text { get; private set; }
        public LocalizedText Wap_DD2BetsyBow_Text { get; private set; }
        public LocalizedText Wap_NeutronBow_LoadingText1 { get; private set; }
        public LocalizedText Wap_NeutronBow_LoadingText2 { get; private set; }
        public LocalizedText Wap_NeutronBow_LoadingText3 { get; private set; }
        public LocalizedText Wap_Gladius_Text { get; private set; }
        public LocalizedText Wap_WoodenBoomerang_Text { get; private set; }
        public LocalizedText Wap_Flamarang_Text { get; private set; }
        public LocalizedText Wap_EnchantedBoomerang_Text { get; private set; }
        public LocalizedText Wap_Shroomerang_Text { get; private set; }
        public LocalizedText Wap_Spear_Text { get; private set; }
        public LocalizedText Wap_VortexBeater_Text { get; private set; }
        public LocalizedText Wap_ChainGun_Text { get; private set; }
        public LocalizedText Wap_MoltenFury_Text { get; private set; }
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

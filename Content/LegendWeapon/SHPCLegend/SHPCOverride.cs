using CalamityMod.Items.Materials;
using CalamityMod.Items.Weapons.Magic;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.RemakeItems.Core;
using System;
using System.Collections.Generic;
using System.Reflection;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using static CalamityOverhaul.Content.InWorldBossPhase;
using static CalamityOverhaul.Content.RemakeItems.Core.ItemRebuildLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend
{
    internal class SHPCOverride : ItemOverride, ICWRLoader
    {
        /// <summary>
        /// 每个时期阶段对应的伤害，这个成员一般不需要直接访问，而是使用<see cref="GetOnDamage"/>
        /// </summary>
        private static Dictionary<int, int> DamageDictionary = new Dictionary<int, int>();
        /// <summary>
        /// 获取开局的伤害
        /// </summary>
        public static int GetStartDamage => DamageDictionary[0];
        public static bool IsLegend => Main.zenithWorld || CWRServerConfig.Instance.WeaponEnhancementSystem;
        public override int TargetID => ModContent.ItemType<SHPC>();
        private static void OnSHPCToolFunc(On_ModItem_ModifyTooltips_Delegate orig, object obj, List<TooltipLine> list) { }
        void ICWRLoader.LoadData() {
            MethodInfo methodInfo = typeof(SHPC).GetMethod("ModifyTooltips", BindingFlags.Public | BindingFlags.Instance);
            CWRHook.Add(methodInfo, OnSHPCToolFunc);
        }
        /// <summary>
        /// 获得成长等级
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public static int GetLevel(Item item) {
            if (item.type != ModContent.ItemType<SHPC>()) {
                return 0;
            }
            CWRItems cwrItem = item.CWR();
            if (cwrItem == null) {
                return 0;
            }
            if (cwrItem.LegendData == null) {
                return 0;
            }
            if (!CWRServerConfig.Instance.WeaponEnhancementSystem) {
                return 12;
            }
            return cwrItem.LegendData.Level;
        }
        /// <summary>
        /// 获取时期对应的伤害
        /// </summary>
        public static int GetOnDamage(Item item) => DamageDictionary[GetLevel(item)];
        public static void LoadWeaponData() {
            DamageDictionary = new Dictionary<int, int>(){
                {0, 8 },
                {1, 10 },
                {2, 16 },
                {3, 20 },
                {4, 28 },
                {5, 36 },
                {6, 44 },
                {7, 66 },
                {8, 80 },
                {9, 100 },
                {10, 150 },
                {11, 180 },
                {12, 240 },
                {13, 360 },
                {14, 1200 }
            };
        }

        public override void SetStaticDefaults() => ItemID.Sets.ShimmerTransformToItem[TargetID] = ModContent.ItemType<PlasmaDriveCore>();
        public override void SetDefaults(Item item) => SetDefaultsFunc(item);
        public override bool On_ModifyWeaponDamage(Item item, Player player, ref StatModifier damage) => SHPCDamage(item, ref damage);
        public override void ModifyTooltips(Item item, List<TooltipLine> tooltips) => SetTooltip(item, ref tooltips);

        public static void SetDefaultsFunc(Item Item) {
            LoadWeaponData();
            Item.damage = GetStartDamage;
            Item.SetHeldProj<SHPCHeld>();
            Item.CWR().LegendData = new SHPCData();
        }

        public static bool SHPCDamage(Item Item, ref StatModifier damage) {
            CWRUtils.ModifyLegendWeaponDamageFunc(Item, GetOnDamage(Item), GetStartDamage, ref damage);
            return false;
        }

        public static void SetTooltip(Item item, ref List<TooltipLine> tooltips) {
            int index = SHPC_Level();
            string newContent = index >= 0 && index <= 14 ? CWRLocText.GetTextValue($"SHPC_TextDictionary_Content_{index}") : "ERROR";
            if (CWRServerConfig.Instance.WeaponEnhancementSystem) {
                string num = (index + 1).ToString();
                if (index == 14) {
                    num = CWRLocText.GetTextValue("Murasama_Text_Lang_End");
                }

                string text = LegendData.GetLevelTrialPreText(item.CWR(), "Murasama_Text_Lang_0", num);

                tooltips.ReplaceTooltip("[Lang4]", text, "");
                tooltips.ReplaceTooltip("legend_Text", CWRLocText.GetTextValue("SHPC_No_legend_Content_3"), "");
            }
            else {
                tooltips.ReplaceTooltip("[Lang4]", "", "");
                tooltips.ReplaceTooltip("legend_Text", CWRLocText.GetTextValue("SHPC_No_legend_Content_4"), "");
                newContent = Level11 ? CWRLocText.GetTextValue("SHPC_No_legend_Content_2") : CWRLocText.GetTextValue("SHPC_No_legend_Content_1");
            }
            Color newColor = Color.Lerp(Color.IndianRed, Color.White, 0.5f + (float)Math.Sin(Main.GlobalTimeWrappedHourly) * 0.5f);
            tooltips.ReplaceTooltip("[Text]", VaultUtils.FormatColorTextMultiLine(newContent, newColor), "");
        }
    }
}

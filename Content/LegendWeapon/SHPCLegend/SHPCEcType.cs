using CalamityMod.Items.Materials;
using CalamityMod.Items.Weapons.Magic;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items;
using CalamityOverhaul.Content.RemakeItems.Core;
using log4net.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using static CalamityOverhaul.Content.InWorldBossPhase;
using static CalamityOverhaul.Content.RemakeItems.Core.ItemRebuildLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend
{
    internal class SHPCEcType : EctypeItem, ICWRLoader
    {
        /// <summary>
        /// 每个时期阶段对应的伤害，这个成员一般不需要直接访问，而是使用<see cref="GetOnDamage"/>
        /// </summary>
        private static Dictionary<int, int> DamageDictionary = new Dictionary<int, int>();
        /// <summary>
        /// 获取开局的伤害
        /// </summary>
        public static int GetStartDamage => DamageDictionary[0];
        /// <summary>
        /// 获取时期对应的伤害
        /// </summary>
        public static int GetOnDamage => DamageDictionary[Instance.SHPC_Level()];
        public override string Texture => CWRConstant.Cay_Wap_Magic + "SHPC";
        public static bool IsLegend => Main.zenithWorld || CWRServerConfig.Instance.WeaponEnhancementSystem;
        private static void onSHPCToolFunc(On_ModItem_ModifyTooltips_Delegate orig, object obj, List<TooltipLine> list) { }
        void ICWRLoader.LoadData() {
            MethodInfo methodInfo = typeof(SHPC).GetMethod("ModifyTooltips", BindingFlags.Public | BindingFlags.Instance);
            CWRHook.Add(methodInfo, onSHPCToolFunc);
        }
        public static void LoadWeaponData() {
            DamageDictionary = new Dictionary<int, int>(){
                {0, 8 },
                {1, 10 },
                {2, 15 },
                {3, 18 },
                {4, 26 },
                {5, 32 },
                {6, 40 },
                {7, 50 },
                {8, 66 },
                {9, 90 },
                {10, 130 },
                {11, 160 },
                {12, 200 },
                {13, 320 },
                {14, 1200 }
            };
        }
        public override void SetStaticDefaults() => SetDefaultsFunc(Item);
        public override void SetDefaults() {
            Item.SetItemCopySD<SHPC>();
            SetDefaultsFunc(Item);
        }
        public static void SetDefaultsFunc(Item Item) {
            LoadWeaponData();
            Item.damage = GetStartDamage;
            Item.SetHeldProj<SHPCHeld>();
        }

        public static bool SHPCDamage(Player player, Item Item, ref StatModifier damage) {
            if (!IsLegend) {
                return false;
            }
            CWRUtils.ModifyLegendWeaponDamageFunc(player, Item, GetOnDamage, GetStartDamage, ref damage);
            return false;
        }

        public override void ModifyWeaponDamage(Player player, ref StatModifier damage) => SHPCDamage(player, Item, ref damage);

        public override void ModifyTooltips(List<TooltipLine> tooltips) => SetTooltip(ref tooltips);

        public static void SetTooltip(ref List<TooltipLine> tooltips) {
            int index = Instance.SHPC_Level();
            string newContent = index >= 0 && index <= 14 ? CWRLocText.GetTextValue($"SHPC_TextDictionary_Content_{index}") : "ERROR";
            if (CWRServerConfig.Instance.WeaponEnhancementSystem) {
                int level = Instance.SHPC_Level();
                string num = (level + 1).ToString();
                if (level == 14) {
                    num = CWRLocText.GetTextValue("Murasama_Text_Lang_End");
                }
                tooltips.ReplaceTooltip("[Lang4]", $"[c/00736d:{CWRLocText.GetTextValue("Murasama_Text_Lang_0") + " "}{num}]", "");
                tooltips.ReplaceTooltip("legend_Text", CWRLocText.GetTextValue("SHPC_No_legend_Content_3"), "");
            }
            else {
                tooltips.ReplaceTooltip("[Lang4]", "", "");
                tooltips.ReplaceTooltip("legend_Text", CWRLocText.GetTextValue("SHPC_No_legend_Content_4"), "");
                newContent = Level11 ? CWRLocText.GetTextValue("SHPC_No_legend_Content_2") : CWRLocText.GetTextValue("SHPC_No_legend_Content_1");
            }
            Color newColor = Color.Lerp(Color.IndianRed, Color.White, 0.5f + (float)Math.Sin(Main.GlobalTimeWrappedHourly) * 0.5f);
            tooltips.ReplaceTooltip("[Text]", CWRUtils.FormatColorTextMultiLine(newContent, newColor), "");
        }
    }

    internal class RSHPC : BaseRItem, ICWRLoader
    {
        public override int TargetID => ModContent.ItemType<SHPC>();
        public override int ProtogenesisID => ModContent.ItemType<SHPCEcType>();
        public override string TargetToolTipItemName => "SHPCEcType";
        public override void SetStaticDefaults() => ItemID.Sets.ShimmerTransformToItem[TargetID] = ModContent.ItemType<PlasmaDriveCore>();
        public override void SetDefaults(Item item) => SHPCEcType.SetDefaultsFunc(item);
        public override bool On_ModifyWeaponDamage(Item item, Player player, ref StatModifier damage) => SHPCEcType.SHPCDamage(player, item, ref damage);
        public override void ModifyTooltips(Item item, List<TooltipLine> tooltips) => SHPCEcType.SetTooltip(ref tooltips);
    }
}

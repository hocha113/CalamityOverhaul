using CalamityMod.Items.Weapons.Magic;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Magic.HeldProjs;
using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic
{
    internal class SHPCEcType : EctypeItem
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
        public static int GetOnDamage => DamageDictionary[InWorldBossPhase.Instance.SHPC_Level()];
        public override string Texture => CWRConstant.Cay_Wap_Magic + "SHPC";
        public static bool IsLegend => Main.zenithWorld || CWRServerConfig.Instance.WeaponEnhancementSystem;
        public static void LoadWeaponData() {
            DamageDictionary = new Dictionary<int, int>(){
                {0, 8 },
                {1, 10 },
                {2, 15 },
                {3, 18 },
                {4, 26 },
                {5, 32 },
                {6, 40 },
                {7, 60 },
                {8, 86 },
                {9, 110 },
                {10, 160 },
                {11, 210 },
                {12, 280 },
                {13, 420 },
                {14, 1300 }
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
            Item.SetHeldProj<SHPCHeldProj>();
        }

        public static void SHPCDamage(Player player, Item Item, ref StatModifier damage) {
            if (!IsLegend) {
                return;
            }
            CWRUtils.ModifyLegendWeaponDamageFunc(player, Item, GetOnDamage, GetStartDamage, ref damage);
        }

        public override void ModifyWeaponDamage(Player player, ref StatModifier damage) => SHPCDamage(player, Item, ref damage);

        public override void ModifyTooltips(List<TooltipLine> tooltips) {
            TooltipLine legendtops = tooltips.FirstOrDefault((TooltipLine x) => x.Text.Contains("[Text]") && x.Mod == "Terraria");
            if (legendtops != null) {
                int index = InWorldBossPhase.Instance.SHPC_Level();
                legendtops.Text = index >= 0 && index <= 14 ? CWRLocText.GetTextValue($"SHPC_TextDictionary_Content_{index}") : "ERROR";

                if (!CWRServerConfig.Instance.WeaponEnhancementSystem) {
                    legendtops.Text = InWorldBossPhase.Instance.level11 ? CWRLocText.GetTextValue("SHPC_No_legend_Content_2") : CWRLocText.GetTextValue("SHPC_No_legend_Content_1");
                }
                legendtops.OverrideColor = Color.Lerp(Color.BlueViolet, Color.White, 0.5f + (float)Math.Sin(Main.GlobalTimeWrappedHourly) * 0.5f);
            }
            SetTooltip(ref tooltips);
        }

        public static void SetTooltip(ref List<TooltipLine> tooltips, string modName = "Terraria") {
            if (CWRServerConfig.Instance.WeaponEnhancementSystem) {
                int level = InWorldBossPhase.Instance.SHPC_Level();
                string num = (level + 1).ToString();
                if (level == 14) {
                    num = CWRLocText.GetTextValue("Murasama_Text_Lang_End");
                }
                tooltips.ReplaceTooltip("[Lang4]", $"[c/00736d:{CWRLocText.GetTextValue("Murasama_Text_Lang_0") + " "}{num}]", modName);
                tooltips.ReplaceTooltip("legend_Text", CWRLocText.GetTextValue("SHPC_No_legend_Content_3"), modName);
            }
            else {
                tooltips.ReplaceTooltip("[Lang4]", "", modName);
                tooltips.ReplaceTooltip("legend_Text", CWRLocText.GetTextValue("SHPC_No_legend_Content_4"), modName);
            }
        }
    }
}

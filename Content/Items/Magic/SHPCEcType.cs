using CalamityMod.Items.Weapons.Magic;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Magic.HeldProjs;
using Microsoft.Xna.Framework;
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
        private static Dictionary<int, int> DamageDictionary => new Dictionary<int, int>(){
            {0, 8 },
            {1, 10 },
            {2, 15 },
            {3, 20 },
            {4, 30 },
            {5, 35 },
            {6, 50 },
            {7, 80 },
            {8, 110 },
            {9, 130 },
            {10, 160 },
            {11, 260 },
            {12, 322 },
            {13, 400 },
            {14, 620 }
        };
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
        public override void SetDefaults() {
            Item.SetCalamitySD<SHPC>();
            Item.damage = GetStartDamage;
            Item.SetHeldProj<SHPCHeldProj>();
        }

        public static void SHPCDamage(ref StatModifier damage) {
            if (IsLegend) {
                damage *= GetOnDamage / (float)GetStartDamage;
            }
        }

        public override void ModifyWeaponDamage(Player player, ref StatModifier damage) {
            SHPCDamage(ref damage);
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips) {
            TooltipLine legendtops = tooltips.FirstOrDefault((TooltipLine x) => x.Text.Contains("[Text]") && x.Mod == "Terraria");
            if (legendtops != null) {
                int index = InWorldBossPhase.Instance.SHPC_Level();
                if (index >= 0 && index <= 14) {
                    legendtops.Text = CWRLocText.GetTextValue($"SHPC_TextDictionary_Content_{index}");
                }
                else {
                    legendtops.Text = "ERROR";
                }

                if (!CWRServerConfig.Instance.WeaponEnhancementSystem) {
                    legendtops.Text = InWorldBossPhase.Instance.level11 ? CWRLocText.GetTextValue("SHPC_No_legend_Content_2") : CWRLocText.GetTextValue("SHPC_No_legend_Content_1");
                }
                legendtops.OverrideColor = Color.Lerp(Color.BlueViolet, Color.White, 0.5f + (float)Math.Sin(Main.GlobalTimeWrappedHourly) * 0.5f);
            }
            SetTooltip(ref tooltips);
        }

        public static void SetTooltip(ref List<TooltipLine> tooltips, string modName = "Terraria") {
            if (CWRServerConfig.Instance.WeaponEnhancementSystem) {
                tooltips.ReplaceTooltip("[Lang4]", $"[c/00736d:{CWRLocText.GetTextValue("Murasama_Text_Lang_0") + " "}{InWorldBossPhase.Instance.SHPC_Level() + 1}]", modName);
                tooltips.ReplaceTooltip("legend_Text", CWRLocText.GetTextValue("SHPC_No_legend_Content_3"), modName);
            }
            else {
                tooltips.ReplaceTooltip("[Lang4]", "", modName);
                tooltips.ReplaceTooltip("legend_Text", CWRLocText.GetTextValue("SHPC_No_legend_Content_4"), modName);
            }
        }
    }
}

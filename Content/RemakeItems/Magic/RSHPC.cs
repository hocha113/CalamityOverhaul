using CalamityMod.Items.Weapons.Magic;
using CalamityOverhaul.Content.Items.Magic;
using CalamityOverhaul.Content.Projectiles.Weapons.Magic.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria.ModLoader;
using Terraria;
using CalamityMod;
using CalamityOverhaul.Common;
using System.Collections.Generic;
using CalamityOverhaul.Content.Items.Melee;
using System.Linq;
using System;
using Terraria.Localization;
using Microsoft.Xna.Framework;
using Mono.Cecil;

namespace CalamityOverhaul.Content.RemakeItems.Magic
{
    internal class RSHPC : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<SHPC>();
        public override int ProtogenesisID => ModContent.ItemType<SHPCEcType>();
        public override string TargetToolTipItemName => "SHPCEcType";
        public override void SetDefaults(Item item) => item.SetHeldProj<SHPCHeldProj>();
        public override bool On_ModifyWeaponDamage(Item item, Player player, ref StatModifier damage) {
            if (SHPCEcType.IsLegend) {
                bool plantera = NPC.downedPlantBoss;
                bool golem = NPC.downedGolemBoss;
                bool cultist = NPC.downedAncientCultist;
                bool moonLord = NPC.downedMoonlord;
                bool providence = DownedBossSystem.downedProvidence;
                bool devourerOfGods = DownedBossSystem.downedDoG;
                bool yharon = DownedBossSystem.downedYharon;
                float damageMult = 1f +
                    (plantera ? 0.1f : 0f) + //1.1
                    (golem ? 0.15f : 0f) + //1.25
                    (cultist ? 3.5f : 0f) + //4.75
                    (moonLord ? 4.5f : 0f) + //9.25
                    (providence ? 7.5f : 0f) + //16.75
                    (devourerOfGods ? 2.5f : 0f) + //19.25
                    (yharon ? 30f : 0f); //49.25
                damage *= damageMult;
            }
            return false;
        }
        public override void ModifyTooltips(Item item, List<TooltipLine> tooltips) {
            // 创建一个新的集合以防修改 tooltips 集合时产生异常
            List<TooltipLine> newTooltips = new List<TooltipLine>(tooltips);
            List<TooltipLine> prefixTooltips = new List<TooltipLine>();
            // 遍历 tooltips 集合并隐藏特定的提示行
            foreach (TooltipLine line in newTooltips.ToList()) {
                for (int i = 0; i < 9; i++) {
                    if (line.Name == "Tooltip" + i) {
                        line.Hide();
                    }
                }
                if (line.Name.Contains("Prefix")) {
                    prefixTooltips.Add(line.Clone());
                    line.Hide();
                }
                if (line.Text == "[GFB]") {
                    line.Hide();
                }
            }

            // 获取自定义的文本内容
            string textContent = Language.GetText("Mods.CalamityOverhaul.Items.SHPCEcType.Tooltip").Value;
            // 拆分传奇提示行的文本内容
            string[] legendtopsList = textContent.Split("\n");
            // 遍历传奇提示行并添加新的提示行
            foreach (string legendtops in legendtopsList) {
                string text = legendtops;
                TooltipLine newLine = new TooltipLine(CWRMod.Instance, "CWRText", text);
                if (newLine.Text == "[GFB]") {
                    newLine.Text = CWRLocText.GetTextValue("SHPC_No_legend_Content_1");
                }
                if (SHPCEcType.IsLegend) {
                    TooltipLine newLine2 = new TooltipLine(CWRMod.Instance, "CWRText", CWRLocText.GetTextValue("SHPC_No_legend_Content_2"));
                    newTooltips.Add(newLine2);
                }
                // 将新提示行添加到新集合中
                newTooltips.Add(newLine);
            }
            // 清空原 tooltips 集合并添加修改后的新Tooltips集合
            tooltips.Clear();
            tooltips.AddRange(newTooltips);
            tooltips.AddRange(prefixTooltips);
        }
    }
}

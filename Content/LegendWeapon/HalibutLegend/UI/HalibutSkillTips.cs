using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.UI
{
    /// <summary>
    /// 给"可研究的鱼"物品悬浮提示注入研究状态行（由 <see cref="CWRItem"/> 调用）
    /// </summary>
    internal static class HalibutSkillTips
    {
        public static void FishSkillTooltip(Item item, List<TooltipLine> tooltips) {
            if (!Main.LocalPlayer.TryGetOverride<HalibutPlayer>(out var halibutPlayer) || !halibutPlayer.HasHalubut) {
                return;
            }
            if (!FishSkill.UnlockFishs.TryGetValue(item.type, out FishSkill fishSkill)) {
                return;
            }
            //水色渐变、柔和高对比
            float ft = (Main.LocalPlayer.miscCounter % 120) / 120f;
            float wave = (float)Math.Sin(ft * MathHelper.TwoPi) * 0.5f + 0.5f;
            Color mainA = new Color(40, 140, 190);
            Color mainB = new Color(120, 230, 255);
            Color accent = Color.Lerp(mainA, mainB, wave);
            Color accent2 = Color.Lerp(mainA, mainB, 0.35f + wave * 0.3f);

            bool unlock = false;
            if (Main.LocalPlayer.TryGetModPlayer<HalibutSave>(out var save)) {
                unlock = save.IsUnlocked(fishSkill);
            }

            var line = new TooltipLine(CWRMod.Instance, "FishSkillTooltip"
                , unlock ? HalibutOverride.FishOnStudied.Value : HalibutOverride.FishByStudied.Value) {
                OverrideColor = accent
            };
            tooltips.Add(line);

            line = new TooltipLine(CWRMod.Instance, "FishSkillTooltip2", fishSkill.Studied.Value) {
                OverrideColor = accent2
            };
            tooltips.Add(line);
        }
    }
}

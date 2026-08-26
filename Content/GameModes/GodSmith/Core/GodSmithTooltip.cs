using System.Collections.Generic;
using CalamityOverhaul.Content.GameModes.UI;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Core
{
    /// <summary>
    /// 神匠 tooltip 注入小工具：金色标题行去重注入与正文拆行。
    /// 标题行名与武器方案（GodSmithScheme）同名，两边先到先注入互不重复
    /// </summary>
    internal static class GodSmithTooltip
    {
        /// <summary>标题行行名（与武器方案侧保持一致）</summary>
        public const string TitleLineName = "CWR_GodSmithTitle";

        /// <summary>神匠金（与武器方案标题行同款取色）</summary>
        public static Color TitleGold => Color.Lerp(GameModeTheme.GodSmithAccent, GameModeTheme.GodSmithEmber, 0.55f);

        /// <summary>正文暗金</summary>
        public static Color BodyGold => Color.Lerp(TitleGold, new Color(200, 190, 172), 0.62f);

        /// <summary>注入金色「神匠重铸」标题行；本模组已注入过（如武器方案先行）则跳过</summary>
        public static void EnsureTitle(List<TooltipLine> tooltips) {
            foreach (TooltipLine line in tooltips) {
                if (line.Name == TitleLineName && line.Mod == CWRMod.Instance.Name) {
                    return;
                }
            }
            tooltips.Add(new TooltipLine(CWRMod.Instance, TitleLineName, GameModeText.GodSmithRecastTitle.Value) {
                OverrideColor = TitleGold
            });
        }

        /// <summary>按 \n 拆行注入正文（行名 = lineName + 行号），空行跳过</summary>
        public static void AddBodyLines(List<TooltipLine> tooltips, string lineName, string text, Color? color = null) {
            if (string.IsNullOrWhiteSpace(text)) {
                return;
            }
            string[] lines = text.Split('\n');
            for (int i = 0; i < lines.Length; i++) {
                if (string.IsNullOrWhiteSpace(lines[i])) {
                    continue;
                }
                tooltips.Add(new TooltipLine(CWRMod.Instance, lineName + i, lines[i]) {
                    OverrideColor = color ?? BodyGold
                });
            }
        }
    }
}

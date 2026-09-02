using CalamityOverhaul.Content.LegendWeapon;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Rarities
{
    /// <summary>
    /// 把本模组稀有度物品的提示框名称行转交给 <see cref="CWRRarity.DrawName"/>。
    /// tML 对每行都会调 PreDrawTooltipLine，哪怕 ModItem.PreDrawTooltip 已返回 false 接管整块面板，
    /// 所以传奇自绘面板的物品要跳过，否则会在原生坐标再画一遍名字
    /// </summary>
    internal sealed class RarityTooltipRenderer : GlobalItem
    {
        public override bool PreDrawTooltipLine(Item item, DrawableTooltipLine line, ref int yOffset) {
            if (line.Mod != "Terraria" || line.Name != "ItemName" || !RarityNameEffects.Enabled) {
                return true;
            }
            if (item.expert || item.master) {
                return true;
            }
            if (RarityLoader.GetRarity(item.rare) is not CWRRarity rarity) {
                return true;
            }
            if (LegendTooltipPanel.IsPanelItem(item.type)) {
                return true;
            }

            Color color = line.OverrideColor ?? line.Color;
            rarity.DrawName(Main.spriteBatch, item, line.Text, new Vector2(line.X, line.Y), color, line.BaseScale, Main.GlobalTimeWrappedHourly);
            return false;
        }
    }
}

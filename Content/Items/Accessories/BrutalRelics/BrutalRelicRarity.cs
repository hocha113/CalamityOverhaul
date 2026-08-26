using System;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Accessories.BrutalRelics
{
    /// <summary>残酷遗物系列稀有度：名称颜色在深红与鎏金之间缓和脉动</summary>
    internal class BrutalRelicRarity : ModRarity
    {
        /// <summary>深红端点</summary>
        public static readonly Color DeepCrimson = new(200, 30, 45);
        /// <summary>鎏金端点</summary>
        public static readonly Color GildedGold = new(255, 215, 130);

        /// <summary>当前脉动色，系列尾行等处可直接取用</summary>
        public static Color PulseColor
            => Color.Lerp(DeepCrimson, GildedGold, MathF.Sin(Main.GlobalTimeWrappedHourly * 2f) * 0.5f + 0.5f);

        public override Color RarityColor => PulseColor;
    }
}

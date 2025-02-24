using CalamityOverhaul.Content.Tiles;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Painting
{
    internal class CalSelfPortrait : ModItem
    {
        public override string Texture => CWRConstant.Asset + "Painting/CalSelfPortrait";
        public override void SetDefaults() {
            Item.width = 102;
            Item.height = 126;
            Item.maxStack = 9999;
            Item.useTurn = true;
            Item.autoReuse = true;
            Item.useAnimation = 15;
            Item.useTime = 10;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.consumable = true;
            Item.value = Item.buyPrice(0, 2, 0, 0);
            Item.rare = ItemRarityID.Purple;
            Item.createTile = ModContent.TileType<CalSelfPortraitTile>();
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips) => CWRUtils.SetItemLegendContentTops(ref tooltips, Name);
    }
}

using CalamityOverhaul.Content.Tiles;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Painting
{
    internal class WUTIVSelfPortrait : ModItem
    {
        public override string Texture => CWRConstant.Asset + "Painting/WUTIVSelfPortrait";
        public const int DropProbabilityDenominator = 12000;
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
            Item.rare = ItemRarityID.Quest;
            Item.createTile = ModContent.TileType<WUTIVSelfPortraitTile>();
        }
    }
}

using CalamityMod.Rarities;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Placeable
{
    internal class BloodAltar : ModItem
    {
        public override string Texture => CWRConstant.Asset + "Items/Placeable/" + "BloodAltar";
        public override void SetDefaults() {
            Item.width = 26;
            Item.height = 26;
            Item.maxStack = 1;
            Item.useTurn = true;
            Item.autoReuse = true;
            Item.useAnimation = Item.useTime = 15;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.consumable = true;
            Item.createTile = ModContent.TileType<Tiles.BloodAltar>();
            Item.rare = ModContent.RarityType<DarkOrange>();
            Item.value = Terraria.Item.buyPrice(gold: 16);
        }
    }
}

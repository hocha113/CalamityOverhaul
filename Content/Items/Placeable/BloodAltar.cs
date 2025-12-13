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
            Item.rare = ItemRarityID.Cyan;
            Item.value = Terraria.Item.buyPrice(gold: 16);
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient(CWRID.Item_BloodOrb, 5)
                .AddIngredient(CWRID.Item_BloodSample, 5)
                .AddIngredient(ItemID.Vertebrae, 5)
                .AddIngredient(ItemID.SoulofNight, 5)
                .AddIngredient(ItemID.CrimstoneBlock, 50)
                .AddTile(TileID.DemonAltar)
                .Register();
            CreateRecipe()
                .AddIngredient(CWRID.Item_BloodOrb, 5)
                .AddIngredient(CWRID.Item_RottenMatter, 5)
                .AddIngredient(ItemID.RottenChunk, 5)
                .AddIngredient(ItemID.SoulofNight, 5)
                .AddIngredient(ItemID.EbonstoneBlock, 50)
                .AddTile(TileID.DemonAltar)
                .Register();
        }
    }
}

using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Placeable
{
    internal class FoodStallChair : ModItem
    {
        public override string Texture => CWRConstant.Item + "Placeable/" + "FoodStallChair";
        public override void SetDefaults() {
            Item.DefaultToPlaceableTile(ModContent.TileType<Tiles.FoodStallChair>());
            Item.width = 32;
            Item.height = 32;
            Item.value = 1150;
        }
        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient(ItemID.Wood)
                .Register();
        }
    }
}

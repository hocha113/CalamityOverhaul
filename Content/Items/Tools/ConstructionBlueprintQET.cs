using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Tools
{
    internal class ConstructionBlueprintQET : ModItem
    {
        public override string Texture => CWRConstant.Item_Tools + "ConstructionBlueprintQET";

        public override void SetDefaults() {
            Item.width = 28;
            Item.height = 32;
            Item.maxStack = 1;
            Item.value = Item.buyPrice(gold: 1);
            Item.rare = ItemRarityID.LightRed;
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.useTime = 20;
            Item.useAnimation = 20;
            Item.useTurn = true;
            Item.autoReuse = false;
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient(ItemID.Wire, 50)
                .AddIngredient(ItemID.Actuator, 10)
                .AddIngredient(ItemID.Cog, 5)
                .AddTile(TileID.TinkerersWorkbench)
                .Register();
        }
    }
}

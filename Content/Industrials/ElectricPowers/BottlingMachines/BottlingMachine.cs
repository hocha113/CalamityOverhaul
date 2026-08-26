using CalamityOverhaul.Content.Items.Materials;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.BottlingMachines
{
    /// <summary>瓶装机:液体与容器物品的双向桥</summary>
    internal class BottlingMachine : ModItem
    {
        public override string Texture => CWRConstant.Asset + "ElectricPowers/BottlingMachine";
        public override void SetDefaults() {
            Item.width = 32;
            Item.height = 32;
            Item.maxStack = 9999;
            Item.useTurn = true;
            Item.autoReuse = true;
            Item.useAnimation = 15;
            Item.useTime = 10;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.consumable = true;
            Item.value = Item.buyPrice(0, 1, 20, 0);
            Item.rare = ItemRarityID.Blue;
            Item.createTile = ModContent.TileType<BottlingMachineTile>();
            Item.CWR().StorageUE = true;
            Item.CWR().ConsumeUseUE = 500;
        }

        public override void AddRecipes() {
            CreateRecipe().
            AddIngredient<CircuitBoard>(8).
            AddIngredient(ItemID.Bottle, 10).
            AddRecipeGroup(RecipeGroupID.IronBar, 10).
            AddRecipeGroup(CWRCrafted.TinBarGroup, 10).
            AddTile(TileID.Anvils).
            Register();

        }
    }
}

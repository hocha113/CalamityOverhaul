using CalamityOverhaul.Content.Items.Materials;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.FluidPumps
{
    /// <summary>抽液泵:耗电抽取世界液体入内部缓冲,液体网络的源头</summary>
    internal class FluidPump : ModItem
    {
        public override string Texture => CWRConstant.Asset + "ElectricPowers/FluidPump";
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
            Item.value = Item.buyPrice(0, 1, 0, 0);
            Item.rare = ItemRarityID.Blue;
            Item.createTile = ModContent.TileType<FluidPumpTile>();
            Item.CWR().StorageUE = true;
            Item.CWR().ConsumeUseUE = 500;
        }

        public override void AddRecipes() {
            CreateRecipe().
            AddIngredient<CircuitBoard>(8).
            AddRecipeGroup(RecipeGroupID.IronBar, 12).
            AddRecipeGroup(CWRCrafted.TinBarGroup, 8).
            AddIngredient(ItemID.Glass, 10).
            AddTile(TileID.Anvils).
            Register();

        }
    }
}

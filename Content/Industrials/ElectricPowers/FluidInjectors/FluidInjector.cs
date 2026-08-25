using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.FluidInjectors
{
    /// <summary>灌注机:抽液泵的逆向,消耗储液向世界放液(占位贴图沿用热力发电机)</summary>
    internal class FluidInjector : ModItem
    {
        public override string Texture => CWRConstant.Asset + "Generator/ThermalGenerator";
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
            Item.createTile = ModContent.TileType<FluidInjectorTile>();
            Item.CWR().StorageUE = true;
            Item.CWR().ConsumeUseUE = 500;
        }

        public override void AddRecipes() {
            if (CWRID.DubiousCircuitryAvailable) {
                CreateRecipe().
                AddIngredient(CWRID.Item_DubiousPlating, 8).
                AddIngredient(CWRID.Item_MysteriousCircuitry, 8).
                AddRecipeGroup(RecipeGroupID.IronBar, 12).
                AddRecipeGroup(CWRCrafted.TinBarGroup, 8).
                AddIngredient(ItemID.Glass, 10).
                AddTile(TileID.Anvils).
                Register();
            }
            else {
                CreateRecipe().
                AddRecipeGroup(RecipeGroupID.IronBar, 12).
                AddRecipeGroup(CWRCrafted.TinBarGroup, 8).
                AddIngredient(ItemID.Glass, 10).
                AddTile(TileID.Anvils).
                Register();
            }
        }
    }
}

using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.Lumberjacks
{
    internal class Lumberjack : ModItem
    {
        public override string Texture => CWRConstant.Asset + "ElectricPowers/Lumberjack";
        internal static LocalizedText Text1;
        internal static LocalizedText Text2;

        public override void SetStaticDefaults() {
            Text1 = this.GetLocalization(nameof(Text1), () => "Excessive Quantity!");
            Text2 = this.GetLocalization(nameof(Text2), () => "Lack of Electricity!");
        }

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
            Item.value = Item.buyPrice(0, 1, 50, 0);
            Item.rare = ItemRarityID.Orange;
            Item.createTile = ModContent.TileType<LumberjackTile>();
            Item.CWR().StorageUE = true;
            Item.CWR().ConsumeUseUE = 500;
        }

        public override void AddRecipes() {
            if (!CWRRef.Has) {
                CreateRecipe().
                AddRecipeGroup(RecipeGroupID.IronBar, 10).
                AddIngredient(ItemID.Sawmill).
                AddIngredient(ItemID.Chain, 5).
                AddTile(TileID.Anvils).
                Register();
                return;
            }
            CreateRecipe().
                AddIngredient(CWRID.Item_DubiousPlating, 10).
                AddIngredient(CWRID.Item_MysteriousCircuitry, 12).
                AddRecipeGroup(RecipeGroupID.IronBar, 10).
                AddIngredient(ItemID.Sawmill).
                AddIngredient(ItemID.Chain, 5).
                AddTile(TileID.Anvils).
                Register();
        }
    }
}

using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.LifeWeavers
{
    internal class LifeWeaver : ModItem
    {
        public override string Texture => CWRConstant.Asset + "ElectricPowers/LifeWeaver";
        internal static LocalizedText NoValidPositionText;
        internal static LocalizedText NoEnergyText;

        public override void SetStaticDefaults() {
            NoValidPositionText = this.GetLocalization(nameof(NoValidPositionText), () => "No Valid Position!");
            NoEnergyText = this.GetLocalization(nameof(NoEnergyText), () => "Lack of Electricity!");
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
            Item.value = Item.buyPrice(0, 1, 0, 0);
            Item.rare = ItemRarityID.Green;
            Item.createTile = ModContent.TileType<LifeWeaverTile>();
            Item.CWR().StorageUE = true;
            Item.CWR().ConsumeUseUE = 200;
        }

        public override void AddRecipes() {
            if (!CWRRef.Has) {
                CreateRecipe().
                AddRecipeGroup(RecipeGroupID.IronBar, 8).
                AddIngredient(ItemID.Acorn, 10).
                AddIngredient(ItemID.Wood, 20).
                AddTile(TileID.Anvils).
                Register();
                return;
            }
            CreateRecipe().
                AddIngredient(CWRID.Item_DubiousPlating, 6).
                AddIngredient(CWRID.Item_MysteriousCircuitry, 8).
                AddRecipeGroup(RecipeGroupID.IronBar, 8).
                AddIngredient(ItemID.Acorn, 10).
                AddIngredient(ItemID.Wood, 20).
                AddTile(TileID.Anvils).
                Register();
        }
    }
}

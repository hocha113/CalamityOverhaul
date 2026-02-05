using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.Incinerators
{
    /// <summary>
    /// 热力焚烧炉，用于煅烧物品
    /// </summary>
    internal class Incinerator : ModItem
    {
        public override string Texture => CWRConstant.Asset + "ElectricPowers/Incinerator";
        public override void SetDefaults() {
            Item.width = 48;
            Item.height = 48;
            Item.maxStack = 9999;
            Item.useTurn = true;
            Item.autoReuse = true;
            Item.useAnimation = 15;
            Item.useTime = 10;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.consumable = true;
            Item.value = Item.buyPrice(0, 1, 20, 0);
            Item.rare = ItemRarityID.Orange;
            Item.createTile = ModContent.TileType<IncineratorTile>();
            Item.CWR().StorageUE = true;
            Item.CWR().ConsumeUseUE = 500;
        }

        public override void AddRecipes() {
            if (!CWRRef.Has) {
                CreateRecipe()
                    .AddIngredient(ItemID.Furnace)
                    .AddRecipeGroup(CWRCrafted.TinBarGroup, 20)
                    .AddRecipeGroup(RecipeGroupID.IronBar, 15)
                    .AddRecipeGroup(CWRCrafted.GoldBarGroup, 15)
                    .AddTile(TileID.Anvils)
                    .Register();
                return;
            }
            CreateRecipe()
                .AddIngredient(ItemID.Furnace)
                .AddRecipeGroup(CWRCrafted.TinBarGroup, 20)
                .AddRecipeGroup(RecipeGroupID.IronBar, 15)
                .AddRecipeGroup(CWRCrafted.GoldBarGroup, 15)
                .AddIngredient(CWRID.Item_DubiousPlating, 8)
                .AddIngredient(CWRID.Item_MysteriousCircuitry, 8)
                .AddTile(TileID.Anvils)
                .Register();
        }
    }
}
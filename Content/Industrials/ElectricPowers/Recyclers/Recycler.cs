using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.Recyclers
{
    /// <summary>回收机:武器/盔甲/饰品按稀有度拆解成锭</summary>
    internal class Recycler : ModItem
    {
        //占位:复用收集者物品贴图,专属贴图见美术清单
        public override string Texture => CWRConstant.Asset + "ElectricPowers/Collector";
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
            Item.createTile = ModContent.TileType<RecyclerTile>();
            Item.CWR().StorageUE = true;
            Item.CWR().ConsumeUseUE = 500;
        }

        public override void AddRecipes() {
            if (CWRID.DubiousCircuitryAvailable) {
                CreateRecipe()
                    .AddRecipeGroup(RecipeGroupID.IronBar, 12)
                    .AddRecipeGroup(CWRCrafted.GoldBarGroup, 8)
                    .AddIngredient(ItemID.Chain, 6)
                    .AddIngredient(CWRID.Item_DubiousPlating, 12)
                    .AddIngredient(CWRID.Item_MysteriousCircuitry, 12)
                    .AddTile(TileID.Anvils)
                    .Register();
            }
            else {
                CreateRecipe()
                    .AddRecipeGroup(RecipeGroupID.IronBar, 12)
                    .AddRecipeGroup(CWRCrafted.GoldBarGroup, 8)
                    .AddIngredient(ItemID.Chain, 6)
                    .AddTile(TileID.Anvils)
                    .Register();
            }
        }
    }
}

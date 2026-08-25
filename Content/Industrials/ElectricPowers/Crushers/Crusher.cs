using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.Crushers
{
    /// <summary>矿石粉碎机:2 矿粉碎成 3 矿,焚化炉的上游增产机</summary>
    internal class Crusher : ModItem
    {
        //占位:复用焚化炉物品贴图,专属贴图见美术清单
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
            Item.createTile = ModContent.TileType<CrusherTile>();
            Item.CWR().StorageUE = true;
            Item.CWR().ConsumeUseUE = 500;
        }

        public override void AddRecipes() {
            if (CWRID.DubiousCircuitryAvailable) {
                CreateRecipe()
                    .AddRecipeGroup(RecipeGroupID.IronBar, 12)
                    .AddRecipeGroup(CWRCrafted.TinBarGroup, 10)
                    .AddIngredient(ItemID.StoneBlock, 30)
                    .AddIngredient(CWRID.Item_DubiousPlating, 10)
                    .AddIngredient(CWRID.Item_MysteriousCircuitry, 10)
                    .AddTile(TileID.Anvils)
                    .Register();
            }
            else {
                CreateRecipe()
                    .AddRecipeGroup(RecipeGroupID.IronBar, 12)
                    .AddRecipeGroup(CWRCrafted.TinBarGroup, 10)
                    .AddIngredient(ItemID.StoneBlock, 30)
                    .AddTile(TileID.Anvils)
                    .Register();
            }
        }
    }
}

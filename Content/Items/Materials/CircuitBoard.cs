using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Materials
{
    /// <summary>电路板:工业机器的统一电子元件材料,双获取路径(灾厄部件换制/原版材料直造)</summary>
    internal class CircuitBoard : ModItem
    {
        public override string Texture => CWRConstant.Item_Material + "CircuitBoard";

        public override void SetDefaults() {
            Item.width = 30;
            Item.height = 32;
            Item.maxStack = 9999;
            Item.value = Item.sellPrice(0, 0, 8, 0);
            Item.rare = ItemRarityID.Blue;
        }

        public override void AddRecipes() {
            if (CWRID.DubiousCircuitryAvailable) {
                //灾厄在场:德雷东残骸部件一比一换制
                CreateRecipe().
                AddIngredient(CWRID.Item_DubiousPlating).
                AddIngredient(CWRID.Item_MysteriousCircuitry).
                AddTile(TileID.Anvils).
                Register();
            }
            else {
                //无灾厄:原版基础材料直造
                CreateRecipe(2).
                AddRecipeGroup(CWRCrafted.TinBarGroup, 1).
                AddRecipeGroup(RecipeGroupID.IronBar, 1).
                AddIngredient(ItemID.Glass, 2).
                AddTile(TileID.Anvils).
                Register();
            }
        }
    }
}

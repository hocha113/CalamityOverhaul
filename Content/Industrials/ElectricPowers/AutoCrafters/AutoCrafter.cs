using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.AutoCrafters
{
    /// <summary>自动合成台:钉选配方,从近旁存储进料,自动合成</summary>
    internal class AutoCrafter : ModItem
    {
        //占位:复用分光染色机物品贴图,专属贴图见美术清单
        public override string Texture => CWRConstant.Asset + "ElectricPowers/Spectrometer";
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
            Item.value = Item.buyPrice(0, 5, 0, 0);
            Item.rare = ItemRarityID.LightRed;
            Item.createTile = ModContent.TileType<AutoCrafterTile>();
            Item.CWR().StorageUE = true;
            Item.CWR().ConsumeUseUE = 800;
        }

        public override void AddRecipes() {
            //困难模式定位:机械零件价位,加工链三台里最强的一台
            if (CWRID.DubiousCircuitryAvailable) {
                CreateRecipe()
                    .AddRecipeGroup(CWRCrafted.MythrilBarGroup, 12)
                    .AddIngredient(ItemID.SoulofMight, 10)
                    .AddIngredient(ItemID.Wire, 30)
                    .AddIngredient(CWRID.Item_DubiousPlating, 15)
                    .AddIngredient(CWRID.Item_MysteriousCircuitry, 15)
                    .AddTile(TileID.MythrilAnvil)
                    .Register();
            }
            else {
                CreateRecipe()
                    .AddRecipeGroup(CWRCrafted.MythrilBarGroup, 12)
                    .AddIngredient(ItemID.SoulofMight, 10)
                    .AddIngredient(ItemID.Wire, 30)
                    .AddTile(TileID.MythrilAnvil)
                    .Register();
            }
        }
    }
}

using CalamityMod.Items.Materials;
using CalamityMod.Tiles.Furniture.CraftingStations;
using CalamityOverhaul.Content.Tiles;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Materials
{
    internal class PestilenceIngot : ModItem
    {
        public override string Texture => CWRConstant.Item + "Materials/PestilenceIngot";
        public new string LocalizationCategory => "Items.Materials";

        public override void SetStaticDefaults() {
            Item.ResearchUnlockCount = 64;
            ItemID.Sets.SortingPriorityMaterials[Type] = 15;
        }

        public override void SetDefaults() {
            Item.width = Item.height = 25;
            Item.maxStack = 9999;
            Item.rare = ItemRarityID.Lime;
            Item.value = Terraria.Item.sellPrice(gold: 3);
            Item.useAnimation = Item.useTime = 15;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.consumable = true;
            Item.createTile = ModContent.TileType<PestilenceIngotTile>();
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient(ModContent.ItemType<PerennialBar>())
                .AddIngredient(ModContent.ItemType<PlagueCellCanister>(), 2)
                .AddIngredient(ModContent.ItemType<InfectedArmorPlating>(), 2)
                .AddTile(ModContent.TileType<PlagueInfuser>())
                .Register();
        }
    }
}

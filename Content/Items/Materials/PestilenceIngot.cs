using CalamityOverhaul.Content.Tiles;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Materials
{
    internal class PestilenceIngot : ModItem
    {
        public override string Texture => CWRConstant.Item + "Materials/PestilenceIngot";
        public override void SetStaticDefaults() {
            Item.ResearchUnlockCount = 64;
            ItemID.Sets.SortingPriorityMaterials[Type] = 15;
        }

        public override void SetDefaults() {
            Item.width = Item.height = 25;
            Item.maxStack = 9999;
            Item.rare = ItemRarityID.Lime;
            Item.value = Item.sellPrice(gold: 3);
            Item.useAnimation = Item.useTime = 15;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.consumable = true;
            Item.useTurn = true;
            Item.autoReuse = true;
            Item.createTile = ModContent.TileType<PestilenceIngotTile>();
        }

        public override void AddRecipes() {
            if (!CWRRef.Has) {
                CreateRecipe()
                .AddIngredient(ItemID.MythrilBar)
                .AddTile(TileID.Anvils)
                .Register();
                return;
            }
            CreateRecipe()
                .AddIngredient(CWRID.Item_PerennialBar)
                .AddIngredient(CWRID.Item_PlagueCellCanister, 2)
                .AddIngredient(CWRID.Item_InfectedArmorPlating, 2)
                .AddTile(CWRID.Tile_PlagueInfuser)
                .Register();
        }
    }
}

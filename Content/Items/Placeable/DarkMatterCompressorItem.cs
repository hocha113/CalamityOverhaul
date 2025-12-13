using CalamityOverhaul.Content.Tiles;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Placeable
{
    internal class DarkMatterCompressorItem : ModItem
    {
        public override string Texture => CWRConstant.Asset + "Items/Placeable/" + "DarkMatterCompressorItem";
        public override void SetStaticDefaults() {
            ItemID.Sets.AnimatesAsSoul[Type] = true;
            Main.RegisterItemAnimation(Item.type, new DrawAnimationVertical(5, 4));
        }

        public override void SetDefaults() {
            Item.width = 26;
            Item.height = 26;
            Item.maxStack = 1;
            Item.useTurn = true;
            Item.autoReuse = true;
            Item.useAnimation = Item.useTime = 15;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.consumable = true;
            Item.createTile = ModContent.TileType<DarkMatterCompressor>();
            Item.rare = ItemRarityID.Cyan;
            Item.value = Item.buyPrice(gold: 16);
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient(CWRID.Item_DarkPlasma, 5)//暗物质
                .AddIngredient(CWRID.Item_StaticRefiner)
                .AddIngredient(CWRID.Item_ProfanedCrucible)
                .AddIngredient(CWRID.Item_PlagueInfuser)
                .AddIngredient(CWRID.Item_MonolithAmalgam)
                .AddIngredient(CWRID.Item_VoidCondenser)
                .Register();
        }
    }
}

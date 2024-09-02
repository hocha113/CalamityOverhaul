using CalamityMod.Items.Materials;
using CalamityMod.Items.Placeables.Furniture.CraftingStations;
using CalamityMod.Rarities;
using CalamityOverhaul.Common;
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
        public override bool IsLoadingEnabled(Mod mod) {
            return !CWRServerConfig.Instance.AddExtrasContent ? false : base.IsLoadingEnabled(mod);
        }
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

            Item.rare = ModContent.RarityType<DarkOrange>();
            Item.value = Terraria.Item.buyPrice(gold: 16);
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient<DarkPlasma>(25)//暗物质
                .AddIngredient<StaticRefiner>()
                .AddIngredient<ProfanedCrucible>()
                .AddIngredient<PlagueInfuser>()
                .AddIngredient<MonolithAmalgam>()
                .AddIngredient<VoidCondenser>()
                .Register();
        }
    }
}

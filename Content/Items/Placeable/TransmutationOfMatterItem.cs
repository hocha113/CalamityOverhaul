using CalamityMod.Items.Materials;
using CalamityMod.Items.Placeables.Furniture.CraftingStations;
using CalamityMod.Rarities;
using CalamityOverhaul.Content.Tiles;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Placeable
{
    internal class TransmutationOfMatterItem : ModItem
    {
        public override string Texture => CWRConstant.Asset + "Items/Placeable/" + "TransmutationOfMatterItem";
        public override void SetDefaults() {
            Item.width = 26;
            Item.height = 26;
            Item.maxStack = 1;
            Item.useTurn = true;
            Item.autoReuse = true;
            Item.useAnimation = Item.useTime = 15;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.consumable = true;
            Item.createTile = ModContent.TileType<TransmutationOfMatter>();
            ItemID.Sets.AnimatesAsSoul[Type] = true;
            Main.RegisterItemAnimation(Type, new DrawAnimationVertical(5, 11));
            Item.rare = ModContent.RarityType<DarkOrange>();
            Item.value = Item.buyPrice(gold: 16);
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient<ShadowspecBar>(5)
                .AddIngredient<DraedonsForge>()
                .AddIngredient<DarkMatterCompressorItem>()
                .Register();
        }
    }
}

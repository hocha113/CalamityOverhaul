using CalamityOverhaul.Content.Tiles;
using CalamityOverhaul.Content.UIs.SupertableUIs;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Materials
{
    internal class DecaySubstance : ModItem
    {
        public override string Texture => CWRConstant.Item + "Materials/DecaySubstance";
        public override void SetStaticDefaults() {
            Item.ResearchUnlockCount = 9999;
            Main.RegisterItemAnimation(Type, new DrawAnimationVertical(5, 5));
            ItemID.Sets.AnimatesAsSoul[Type] = true;
        }
        public override void SetDefaults() {
            Item.width = Item.height = 25;
            Item.maxStack = 9999;
            Item.rare = ItemRarityID.Lime;
            Item.value = Item.sellPrice(gold: 13);
            Item.useAnimation = Item.useTime = 15;
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.CWR().OmigaSnyContent = SupertableRecipeDate.FullItems12;
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient(ItemID.FragmentVortex, 1)
                .AddIngredient(ItemID.FragmentNebula, 1)
                .AddIngredient(ItemID.FragmentSolar, 1)
                .AddIngredient(ItemID.FragmentStardust, 1)
                .AddIngredient(ItemID.LunarBar, 4)
                .AddIngredient<DecayParticles>(1)
                .AddBlockingSynthesisEvent()
                .AddTile(ModContent.TileType<TransmutationOfMatter>())
                .Register();
        }
    }
}

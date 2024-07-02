using CalamityMod.Items.Materials;
using CalamityOverhaul.Content.Projectiles.AmmoBoxs;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Placeable
{
    internal class ArmourPiercerBox : ModItem
    {
        public override string Texture => CWRConstant.Item + "Placeable/HEATBox";
        public override void SetStaticDefaults() {
            Main.RegisterItemAnimation(Type, new DrawAnimationVertical(6, 38));
        }

        public override void SetDefaults() {
            Item.width = Item.height = 32;
            Item.value = 890;
            Item.useTime = Item.useAnimation = 22;
            Item.consumable = true;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.UseSound = SoundID.Item1;
            Item.SetHeldProj<ArmourPiercerHeld>();
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient(ItemID.AmmoBox)
                .AddIngredient(ItemID.EmptyBullet, 100)
                .AddIngredient(ModContent.ItemType<WulfrumMetalScrap>(), 5)
                .AddIngredient(ModContent.ItemType<DubiousPlating>(), 5)
                .AddTile(TileID.Anvils)
                .Register();
        }
    }
}

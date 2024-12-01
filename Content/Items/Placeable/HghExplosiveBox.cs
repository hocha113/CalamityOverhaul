using CalamityMod.Items.Materials;
using CalamityOverhaul.Content.Projectiles.AmmoBoxs;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Placeable
{
    internal class HghExplosiveBox : ModItem
    {
        public override string Texture => CWRConstant.Item + "Placeable/HghExplosiveBox";
        public override void SetDefaults() {
            Item.width = Item.height = 32;
            Item.value = 890;
            Item.useTime = Item.useAnimation = 22;
            Item.consumable = true;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.UseSound = SoundID.Item1;
            Item.rare = ItemRarityID.LightPurple;
            Item.SetHeldProj<HghExplosiveHeld>();
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient(ItemID.AmmoBox)
                .AddIngredient(ItemID.EmptyBullet, 50)
                .AddIngredient(ModContent.ItemType<UnholyEssence>(), 2)
                .AddTile(TileID.Anvils)
                .Register();
        }
    }
}

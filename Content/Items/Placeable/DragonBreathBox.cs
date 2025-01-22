using CalamityMod.Items.Materials;
using CalamityOverhaul.Content.Projectiles.AmmoBoxs;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Placeable
{
    internal class DragonBreathBox : ModItem
    {
        public override string Texture => CWRConstant.Item + "Placeable/DBCBox";
        public override void SetDefaults() {
            Item.width = Item.height = 32;
            Item.value = 890;
            Item.useTime = Item.useAnimation = 22;
            Item.consumable = true;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.UseSound = SoundID.Item1;
            Item.rare = ItemRarityID.Yellow;
            Item.maxStack = 64;
            Item.SetHeldProj<DragonBreathHeld>();
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient(ItemID.AmmoBox)
                .AddIngredient(ItemID.EmptyBullet, 50)
                .AddIngredient(ModContent.ItemType<YharonSoulFragment>(), 2)
                .AddTile(TileID.Anvils)
                .Register();
        }
    }
}

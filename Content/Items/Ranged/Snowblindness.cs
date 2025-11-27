using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class Snowblindness : ModItem
    {
        public override string Texture => CWRConstant.Item_Ranged + "Snowblindness";
        public override void SetDefaults() {
            Item.damage = 30;
            Item.DamageType = DamageClass.Ranged;
            Item.width = 84;
            Item.height = 34;
            Item.useTime = Item.useAnimation = 3;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.knockBack = 1.5f;
            Item.value = Terraria.Item.buyPrice(0, 8, 3, 5);
            Item.rare = ItemRarityID.Red;
            Item.UseSound = CWRSound.Gun_Snowblindness_Shoot with { Volume = 0.3f };
            Item.autoReuse = true;
            Item.shoot = ProjectileID.Bullet;
            Item.shootSpeed = 28f;
            Item.crit = 10;
            Item.useAmmo = AmmoID.Snowball;
            Item.SetItemCanFirePointBlankShots(true);
            Item.SetCartridgeGun<SnowblindnessHeld>(500);
        }

        public override void AddRecipes() {
            _ = CreateRecipe().
                AddIngredient<AvalancheM60>().
                AddIngredient(ItemID.LaserRifle).
                AddIngredient(ItemID.FragmentVortex, 6).
                AddTile(TileID.LunarCraftingStation).
                Register();
        }
    }
}

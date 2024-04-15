using CalamityMod;
using CalamityMod.Items;
using CalamityMod.Items.Materials;
using CalamityMod.Rarities;
using CalamityMod.Tiles.Furniture.CraftingStations;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged.Extras
{
    internal class Snowblindness : ModItem
    {
        public override string Texture => CWRConstant.Item_Ranged + "Snowblindness";
        public override void SetDefaults() {
            Item.damage = 45;
            Item.DamageType = DamageClass.Ranged;
            Item.width = 84;
            Item.height = 34;
            Item.useTime = Item.useAnimation = 3;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.knockBack = 4.5f;
            Item.value = Terraria.Item.buyPrice(0, 5, 3, 5);
            Item.rare = ModContent.RarityType<DarkOrange>();
            Item.UseSound = CWRSound.Gun_Snowblindness_Shoot with { Volume = 0.6f };
            Item.autoReuse = true;
            Item.shoot = ProjectileID.Bullet;
            Item.shootSpeed = 28f;
            Item.useAmmo = AmmoID.Snowball;
            Item.Calamity().canFirePointBlankShots = true;
            Item.SetCartridgeGun<SnowblindnessHeldProj>(500);
        }

        public override void AddRecipes() {
            _ = CreateRecipe().
                AddIngredient<AvalancheM60>().
                AddIngredient(ItemID.FragmentVortex, 6).
                AddTile(TileID.LunarCraftingStation).
                Register();
        }
    }
}

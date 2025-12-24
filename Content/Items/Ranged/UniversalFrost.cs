using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged
{
    //万象霜天
    internal class UniversalFrost : ModItem
    {
        public override string Texture => CWRConstant.Item_Ranged + "UniversalFrost";
        public override void SetDefaults() {
            Item.damage = 188;
            Item.DamageType = DamageClass.Ranged;
            Item.width = 96;
            Item.height = 38;
            Item.useTime = Item.useAnimation = 3;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.knockBack = 2.5f;
            Item.value = Item.buyPrice(0, 32, 0, 0);
            Item.rare = ItemRarityID.Purple;
            Item.UseSound = CWRSound.Gun_Snowblindness_Shoot with { Volume = 0.35f };
            Item.autoReuse = true;
            Item.shoot = ProjectileID.Bullet;
            Item.shootSpeed = 32f;
            Item.crit = 12;
            Item.useAmmo = AmmoID.Snowball;
            Item.SetItemCanFirePointBlankShots(true);
            Item.SetCartridgeGun<UniversalFrostHeld>(800);
        }

        public override void AddRecipes() {
            if (!CWRRef.Has) {
                CreateRecipe().
                AddIngredient<CrystalDimming>().
                AddIngredient(ItemID.LunarBar, 8).
                AddTile(TileID.LunarCraftingStation).
                Register();
                return;
            }
            _ = CreateRecipe().
                AddIngredient<CrystalDimming>().
                AddIngredient(CWRID.Item_CosmiliteBar, 5).
                AddIngredient(CWRID.Item_EndothermicEnergy, 20).
                AddIngredient(CWRID.Item_CoreofEleum, 3).
                AddTile(CWRID.Tile_CosmicAnvil).
                Register();
        }
    }
}

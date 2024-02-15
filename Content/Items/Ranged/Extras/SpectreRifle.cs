using CalamityMod;
using CalamityMod.Items;
using CalamityMod.Items.Materials;
using CalamityMod.Rarities;
using CalamityMod.Sounds;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged.Extras
{
    /// <summary>
    /// 幽灵狙击枪
    /// </summary>
    internal class SpectreRifle : ModItem
    {
        public override string Texture => CWRConstant.Item_Ranged + "SpectreRifle";
        public override void SetDefaults() {
            Item.damage = 228;
            Item.DamageType = DamageClass.Ranged;
            Item.width = 124;
            Item.height = 78;
            Item.useTime = 45;
            Item.useAnimation = 45;
            Item.reuseDelay = 15;
            Item.useLimitPerAnimation = 2;
            Item.autoReuse = true;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.knockBack = 1.5f;
            Item.shootSpeed = 15f;
            Item.useAmmo = AmmoID.Bullet;
            Item.UseSound = CommonCalamitySounds.LargeWeaponFireSound with { Volume = 0.6f, Pitch = -0.3f };
            Item.rare = ModContent.RarityType<Violet>();
            Item.value = CalamityGlobalItem.Rarity15BuyPrice;
            Item.Calamity().canFirePointBlankShots = true;
            Item.CWR().heldProjType = ModContent.ProjectileType<SpectreRifleHeldProj>();
            Item.CWR().hasHeldNoCanUseBool = true;
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient(ItemID.SpectreBar, 15)
                .AddIngredient<CoreofEleum>(15)
                .AddTile(TileID.LunarCraftingStation)
                .Register();
        }
    }
}

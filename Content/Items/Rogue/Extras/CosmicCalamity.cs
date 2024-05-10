using CalamityMod.Items;
using CalamityMod;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Rogue;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria;
using Terraria.DataStructures;
using Microsoft.Xna.Framework;
using CalamityMod.Projectiles.Rogue;
using CalamityMod.UI;
using CalamityMod.Items.Materials;
using CalamityMod.Items.Placeables.Furniture.CraftingStations;
using CalamityMod.Tiles.Furniture.CraftingStations;

namespace CalamityOverhaul.Content.Items.Rogue.Extras
{
    internal class CosmicCalamity : ModItem
    {
        public override string Texture => CWRConstant.Item + "Rogue/CosmicCalamity";
        public override void SetDefaults() {
            Item.width = 44;
            Item.damage = 482;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.useAnimation = Item.useTime = 15;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 4f;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.height = 44;
            Item.value = CalamityGlobalItem.RarityPinkBuyPrice;
            Item.rare = ItemRarityID.Pink;
            Item.shoot = ModContent.ProjectileType<CosmicCalamityProjectile>();
            Item.shootSpeed = 12f;
            Item.DamageType = ModContent.GetInstance<RogueDamageClass>();
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (player.Calamity().StealthStrikeAvailable()) {
                int proj = Projectile.NewProjectile(source, position, velocity, ModContent.ProjectileType<CosmicCalamityProjectile>(), damage * 2, knockback, player.whoAmI);
                Main.projectile[proj].Calamity().stealthStrike = true;
                Main.projectile[proj].timeLeft = 600;
                Main.projectile[proj].scale = 1.2f;
                return false;
            }
            return base.Shoot(player, source, position, velocity, type, damage, knockback);
        }

        public override void AddRecipes() {
            CreateRecipe().
                AddIngredient<CosmiliteBar>(12).
                AddIngredient<CalamityMod.Items.Weapons.Rogue.WaveSkipper>().
                AddTile(ModContent.TileType<CosmicAnvil>()).
                Register();
        }
    }
}

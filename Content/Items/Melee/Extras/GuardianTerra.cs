using CalamityMod.Items.Materials;
using CalamityMod.Tiles.Furniture.CraftingStations;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee.Extras
{
    internal class GuardianTerra : ModItem
    {
        public override string Texture => CWRConstant.Item_Melee + "GuardianTerra";
        public override void SetDefaults() {
            Item.width = 62;
            Item.height = 76;
            Item.rare = ItemRarityID.Green;
            Item.value = Item.sellPrice(0, 45, 5, 0);
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.useAnimation = 25;
            Item.useTime = 25;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.damage = 227;
            Item.knockBack = 6;
            Item.noUseGraphic = true;
            Item.DamageType = DamageClass.Melee;
            Item.noMelee = true;
            Item.channel = true;
            Item.shootSpeed = 5f;
            Item.shoot = ModContent.ProjectileType<GuardianTerraHeld>();
            ItemID.Sets.ItemsThatAllowRepeatedRightClick[Type] = true;
        }

        public override void ModifyWeaponCrit(Player player, ref float crit) => crit += 16;

        public override bool AltFunctionUse(Player player) => true;

        public override bool CanUseItem(Player player) => player.ownedProjectileCounts[Item.shoot] <= 0;

        public override void UseStyle(Player player, Rectangle heldItemFrame) => player.itemLocation = player.GetPlayerStabilityCenter();

        public override bool? UseItem(Player player) {
            Item.useAnimation = 15;
            Item.useTime = 15;
            Item.shootSpeed = 15f;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.noUseGraphic = false;
            Item.noMelee = false;
            Item.UseSound = SoundID.Item60 with { Pitch = 0.2f, MaxInstances = 2 };
            if (player.altFunctionUse == 2) {
                Item.useAnimation = 25;
                Item.useTime = 25;
                Item.shootSpeed = 5f;
                Item.UseSound = SoundID.Item1;
                Item.useStyle = ItemUseStyleID.Shoot;
                Item.noUseGraphic = true;
                Item.noMelee = true;
            }
            return base.UseItem(player);
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (player.altFunctionUse == 2) {
                Item.initialize();
                Item.CWR().ai[0]++;
                Projectile.NewProjectile(source, position, velocity, type, damage * 3, knockback, player.whoAmI, Item.CWR().ai[0] % 2 == 0 ? 1 : 0);
                return false;
            }
            for (int i = 0; i < Main.rand.Next(2, 4); i++) {
                Projectile.NewProjectile(source, position + velocity * 2, velocity.RotatedByRandom(0.45f)
                , ModContent.ProjectileType<GuardianTerraBeam>(), damage, knockback, player.whoAmI);
            }
            return false;
        }

        public override void AddRecipes() {
            CreateRecipe().
                AddIngredient<AuricBar>(5).
                AddIngredient<CalamityMod.Items.Weapons.Melee.Terratomere>().
                AddIngredient<CalamityMod.Items.Weapons.Melee.Orderbringer>().
                AddTile(ModContent.TileType<CosmicAnvil>()).
                Register();
        }
    }
}

using CalamityOverhaul.Common;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee.DivineSourceBlades
{
    /// <summary>
    /// 金源灭却刃 —— 背后悬浮巨剑，四段挥斩后轰出巨型新月剑气
    /// </summary>
    internal class DivineSourceBlade : ModItem
    {
        public override string Texture => DivineSourceBladeFX.BladeTexture;

        public override void SetDefaults() {
            Item.width = 100;
            Item.height = 164;
            Item.damage = 1560;
            Item.DamageType = DamageClass.Melee;
            Item.useAnimation = Item.useTime = 52;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 5.5f;
            Item.value = Item.buyPrice(0, 33, 15, 0);
            Item.rare = ItemRarityID.Red;
            Item.autoReuse = true;
            Item.noUseGraphic = true;
            Item.noMelee = true;
            Item.shoot = ModContent.ProjectileType<DivineSourceBladeHeld>();
            Item.shootSpeed = 1f;
            Item.UseSound = null;
        }

        public override bool CanUseItem(Player player) {
            if (player.ownedProjectileCounts[ModContent.ProjectileType<DivineSourceBladeHeld>()] > 0) {
                return false;
            }
            return base.CanUseItem(player);
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback) {

            Vector2 dir = (Main.MouseWorld - player.Center).SafeNormalize(Vector2.UnitX);
            Projectile.NewProjectile(source, player.Center, dir, type, damage, knockback, player.whoAmI);
            return false;
        }

        public override void ModifyWeaponCrit(Player player, ref float crit) => crit += 10;

        public override void AddRecipes() {
            if (!CWRRef.Has) {
                return;
            }
            CreateRecipe().
                AddIngredient(CWRID.Item_AuricBar, 5).
                AddIngredient(CWRID.Item_Terratomere).
                AddIngredient(CWRID.Item_Excelsus).
                AddTile(CWRID.Tile_CosmicAnvil).
                Register();
        }
    }
}

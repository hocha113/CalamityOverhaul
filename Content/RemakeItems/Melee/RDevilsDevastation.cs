using CalamityOverhaul.Content.Projectiles.Weapons.Melee.DevilsDevastationProj;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RDevilsDevastation : CWRItemOverride
    {
        private int Level;
        public override void SetDefaults(Item item) => SetDefaultsFunc(item);
        public override bool? CanUseItem(Item item, Player player) => player.ownedProjectileCounts[item.shoot] <= 0;
        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position
            , Vector2 velocity, int type, int damage, float knockback) {
            return ShootFunc(ref Level, item, player, source, position, velocity, type, damage, knockback);
        }

        internal static void SetDefaultsFunc(Item Item) {
            Item.width = 94;
            Item.height = 80;
            Item.scale = 1f;
            Item.damage = 230;
            Item.DamageType = DamageClass.Melee;
            Item.useAnimation = 20;
            Item.useTime = 20;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.useTurn = true;
            Item.knockBack = 3.75f;
            Item.UseSound = null;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.shoot = ModContent.ProjectileType<DevilsDevastationHeld>();
            Item.shootSpeed = 10f;
        }

        internal static bool ShootFunc(ref int Level, Item Item, Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            int newLevel = 0;
            if (++Level > 9) {
                newLevel = Level - 9;
                if (newLevel > 2) {
                    Level = 0;
                }
            }
            Projectile.NewProjectile(source, position, velocity, ModContent.ProjectileType<DevilsDevastationHeld>(), damage, knockback, player.whoAmI, newLevel);
            return false;
        }
    }
}

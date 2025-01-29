using CalamityMod.Items.Weapons.Melee;
using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RLifehuntScythe : ItemOverride
    {
        public override int TargetID => ModContent.ItemType<LifehuntScythe>();
        private int swingIndex = 0;
        public override void SetDefaults(Item item) {
            item.useTime = item.useAnimation = 22;
            item.UseSound = null;
            item.SetKnifeHeld<LifehuntScytheHeld>();
        }

        public override bool? CanUseItem(Item item, Player player) {
            return player.ownedProjectileCounts[item.shoot] <= 0
                && player.ownedProjectileCounts[ModContent.ProjectileType<LifehuntScytheThrowable>()] <= 0;
        }

        public override void ModifyShootStats(Item item, Player player, ref Vector2 position
            , ref Vector2 velocity, ref int type, ref int damage, ref float knockback) {
            if (++swingIndex > 6) {
                type = ModContent.ProjectileType<LifehuntScytheThrowable>();
                swingIndex = 0;
            }
        }

        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            Projectile.NewProjectile(source, position, velocity, type, damage, knockback
                , player.whoAmI, swingIndex % 2 == 0 ? 0 : 1, swingIndex + 1);
            return false;
        }
    }
}

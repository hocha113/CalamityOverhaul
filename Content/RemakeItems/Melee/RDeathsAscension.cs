using CalamityMod;
using CalamityMod.Items.Weapons.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RDeathsAscension : ItemOverride
    {
        public override int TargetID => ModContent.ItemType<DeathsAscension>();
        private int swingIndex = 0;
        public override void SetDefaults(Item item) {
            swingIndex = 0;
            item.UseSound = null;
            item.DamageType = ModContent.GetInstance<TrueMeleeDamageClass>();
            item.SetKnifeHeld<DeathsAscensionHeld>();
        }

        public override bool? On_UseItem(Item item, Player player) => true;

        public override bool? On_CanUseItem(Item item, Player player) {
            return player.ownedProjectileCounts[item.shoot] <= 0
                && player.ownedProjectileCounts[ModContent.ProjectileType<DeathsAscensionThrowable>()] <= 0;
        }

        public override void ModifyShootStats(Item item, Player player, ref Vector2 position
            , ref Vector2 velocity, ref int type, ref int damage, ref float knockback)
            => ModifyShootStatsFunc(ref swingIndex, item, player
                , ref position, ref velocity, ref type, ref damage, ref knockback);

        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback)
            => ShootFunc(ref swingIndex, item, player, source, position
                , velocity, type, damage, knockback);

        public static void ModifyShootStatsFunc(ref int swingIndex, Item Item, Player player, ref Vector2 position
            , ref Vector2 velocity, ref int type, ref int damage, ref float knockback) {
            Item.useTime = Item.useAnimation = 22;
            Item.GiveMeleeType(true);
            if (player.altFunctionUse == 2) {
                Item.useTime = Item.useAnimation = 16;
                Item.GiveMeleeType();
            }
            else if (++swingIndex > 7) {
                type = ModContent.ProjectileType<DeathsAscensionThrowable>();
                swingIndex = 0;
            }
        }

        public static bool ShootFunc(ref int swingIndex, Item Item, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (player.altFunctionUse == 2) {
                swingIndex = 0;
                Projectile.NewProjectile(source, position, velocity
                    , ModContent.ProjectileType<DeathsAscensionHeld>(), damage / 2, knockback
                , player.whoAmI, 0, 0, 1);
                return false;
            }

            Projectile.NewProjectile(source, position, velocity, type, damage, knockback
                , player.whoAmI, swingIndex % 2 == 0 ? 0 : 1, swingIndex + 1);
            return false;
        }
    }
}

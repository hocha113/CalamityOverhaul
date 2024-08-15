using CalamityMod;
using CalamityMod.Items.Weapons.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee
{
    internal class DeathsAscensionEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "DeathsAscension";

        private int swingIndex = 0;
        public override void SetDefaults() {
            ItemID.Sets.ItemsThatAllowRepeatedRightClick[Type] = true;
            Item.SetCalamitySD<DeathsAscension>();
            Item.DamageType = ModContent.GetInstance<TrueMeleeDamageClass>();
            Item.SetKnifeHeld<DeathsAscensionHeld>();
        }

        public override bool CanUseItem(Player player) {
            return player.ownedProjectileCounts[Item.shoot] <= 0
                && player.ownedProjectileCounts[ModContent.ProjectileType<DeathsAscensionThrowable>()] <= 0;
        }

        public override void ModifyShootStats(Player player, ref Vector2 position
            , ref Vector2 velocity, ref int type, ref int damage, ref float knockback)
            => ModifyShootStatsFunc(ref swingIndex, player, ref position, ref velocity, ref type, ref damage, ref knockback);

        public static void ModifyShootStatsFunc(ref int swingIndex, Player player, ref Vector2 position
            , ref Vector2 velocity, ref int type, ref int damage, ref float knockback) {
            if (player.altFunctionUse == 2) {

            }
            else if (++swingIndex > 7) {
                type = ModContent.ProjectileType<DeathsAscensionThrowable>();
                swingIndex = 0;
            }
        }

        public override bool AltFunctionUse(Player player) => true;

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback)
            => ShootFunc(ref swingIndex, player, source, position, velocity, type, damage, knockback);

        public static bool ShootFunc(ref int swingIndex, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (player.altFunctionUse == 2) {
                swingIndex = 0;
                Projectile.NewProjectile(source, position, velocity
                    , ModContent.ProjectileType<DeathsAscensionHeld>(), damage, knockback
                , player.whoAmI, 0, 0, 1);
                return false;
            }
            Projectile.NewProjectile(source, position, velocity, type, damage, knockback
                , player.whoAmI, swingIndex % 2 == 0 ? 0 : 1, swingIndex + 1);
            return false;
        }
    }
}

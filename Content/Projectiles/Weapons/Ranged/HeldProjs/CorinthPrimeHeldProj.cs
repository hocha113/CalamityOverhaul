using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class CorinthPrimeHeldProj : BaseGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "CorinthPrime";
        public override int targetCayItem => ModContent.ItemType<CalamityMod.Items.Weapons.Ranged.CorinthPrime>();
        public override int targetCWRItem => ModContent.ItemType<CorinthPrimeEcType>();
        public override void SetRangedProperty() {
            ControlForce = 0.1f;
            GunPressure = 0.2f;
            Recoil = 2;
            HandDistance = 27;
            HandDistanceY = 5;
            HandFireDistance = 27;
            HandFireDistanceY = -8;
            CanRightClick = true;
        }

        public override void PostInOwnerUpdate() {
            if (onFireR) {
                GunPressure = 0.7f;
                Recoil = 6;
                RangeOfStress = 25;
                Item.useTime = 60;
            }
            else {
                GunPressure = 0.2f;
                Recoil = 2;
                RangeOfStress = 5;
                Item.useTime = 20;
            }
        }

        public override void FiringShoot() {
            for (int i = 0; i < 6; i++) {
                Projectile.NewProjectile(Owner.parent(), Projectile.Center,
                    ShootVelocity.RotatedBy(Main.rand.NextFloat(-0.1f, 0.1f)) * Main.rand.NextFloat(0.7f, 1.1f)
                    , ModContent.ProjectileType<RealmRavagerBullet>(), WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            }
            _ = UpdateConsumeAmmo();
        }

        public override void FiringShootR() {
            Projectile.NewProjectile(Owner.parent(), Projectile.Center, ShootVelocity
                    , ModContent.ProjectileType<CorinthPrimeAirburstGrenade>(), WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            _ = UpdateConsumeAmmo();
        }
    }
}

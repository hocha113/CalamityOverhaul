using CalamityOverhaul.Content.RangedModify.Core;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class CorinthPrimeHeld : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "CorinthPrime";
        public override void SetRangedProperty() {
            ControlForce = 0.1f;
            GunPressure = 0.2f;
            Recoil = 2;
            HandIdleDistanceX = 27;
            HandIdleDistanceY = 5;
            HandFireDistanceX = 27;
            HandFireDistanceY = -4;
            ShootPosNorlLengValue = -4;
            ShootPosToMouLengValue = 15;
            CanRightClick = true;
            ForcedConversionTargetAmmoFunc = () => AmmoTypes == ProjectileID.Bullet;
            ToTargetAmmo = CWRID.Proj_RealmRavagerBullet;
        }

        public override void PostInOwner() {
            if (onFireR) {
                GunPressure = 0.7f;
                Recoil = 6;
                RangeOfStress = 25;
                FireTime = 80;
            }
            else {
                GunPressure = 0.2f;
                Recoil = 1;
                RangeOfStress = 5;
                FireTime = 30;
            }
        }

        public override void FiringShoot() {
            for (int i = 0; i < 6; i++) {
                Projectile.NewProjectile(Source, Projectile.Center,
                    ShootVelocity.RotatedBy(Main.rand.NextFloat(-0.1f, 0.1f)) * Main.rand.NextFloat(0.7f, 1.1f)
                    , AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            }
        }

        public override void FiringShootR() {
            Projectile.NewProjectile(Source, Projectile.Center, ShootVelocity
                    , CWRID.Proj_CorinthPrimeAirburstGrenade, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
        }
    }
}

using CalamityOverhaul.Content.RangedModify.Core;
using System;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class SomaPrimeHeld : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "SomaPrime";
        public override void SetRangedProperty() {
            KreloadMaxTime = 90;
            FireTime = 2;
            GunPressure = 0;
            ControlForce = 0;
            Recoil = 0.2f;
            RangeOfStress = 25;
            HandIdleDistanceX = 25;
            HandIdleDistanceY = 5;
            HandFireDistanceX = 25;
            HandFireDistanceY = -5;
            ShootPosNorlLengValue = -2;
            ShootPosToMouLengValue = 10;
            RecoilRetroForceMagnitude = 3;
            SpwanGunDustData.splNum = 0.5f;
            RepeatedCartridgeChange = true;
            MustConsumeAmmunition = false;
            EnableRecoilRetroEffect = true;
            SpwanGunDustData.dustID1 = DustID.Adamantite;
            SpwanGunDustData.dustID2 = DustID.SailfishBoots;
            SpwanGunDustData.dustID3 = DustID.UltraBrightTorch;
        }

        public override void FiringShoot() {
            if (AmmoTypes == ProjectileID.Bullet) {
                AmmoTypes = ProjectileID.BulletHighVelocity;
                WeaponDamage += 14;
            }
            int proj = Projectile.NewProjectile(Source, ShootPos, ShootVelocity, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            CWRRef.SetProjCGP(proj);
            float value = MathF.Sin(Time * 0.05f) * 0.3f;
            Vector2 newPos = ShootPos - ShootVelocity.UnitVector() * 46;
            proj = Projectile.NewProjectile(Source, newPos, ShootVelocity.RotatedBy(value), AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            CWRRef.SetProjCGP(proj);
            proj = Projectile.NewProjectile(Source, newPos, ShootVelocity.RotatedBy(-value), AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            CWRRef.SetProjCGP(proj);
        }
    }
}

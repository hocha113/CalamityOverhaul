using CalamityOverhaul.Content.RangedModify.Core;
using Terraria;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class SeadragonHeld : BaseGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Seadragon";
        public override void SetRangedProperty() {
            ControlForce = 0;
            GunPressure = 0;
            Recoil = 0.8f;
            ShootPosToMouLengValue = 12;
            CanCreateSpawnGunDust = false;
        }

        public override void FiringShoot() {
            OffsetPos += ShootVelocity.UnitVector() * -5;
            Vector2 gundir = Projectile.rotation.ToRotationVector2();

            Projectile.NewProjectile(Source, Projectile.Center + gundir * 3, ShootVelocity
                , AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);

            Projectile.NewProjectile(Source2, Projectile.Center + gundir * 3, ShootVelocity, CWRID.Proj_ArcherfishShot, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);

            Projectile.NewProjectile(Source2, Projectile.Center + gundir * 3
                , ShootVelocity.RotatedByRandom(MathHelper.ToRadians(5f)) * Main.rand.NextFloat(1.45f, 1.65f)
                , CWRID.Proj_ArcherfishRing
                , WeaponDamage / 2, WeaponKnockback, Owner.whoAmI);

            _ = UpdateConsumeAmmo();
        }
    }
}

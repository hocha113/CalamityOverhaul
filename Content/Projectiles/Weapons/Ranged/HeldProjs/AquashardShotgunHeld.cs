using CalamityOverhaul.Content.RangedModify.Core;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class AquashardShotgunHeld : BaseGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "AquashardShotgun";
        public override void SetRangedProperty() {
            ControlForce = 0.1f;
            GunPressure = 0.52f;
            Recoil = 4;
        }

        public override void SetShootAttribute() {
            if (AmmoTypes == ProjectileID.Bullet) {
                AmmoTypes = CWRID.Proj_Aquashard;
            }
        }

        public override void FiringShoot() {
            for (int i = 0; i < 4; i++) {
                Projectile.NewProjectile(Source, Projectile.Center, ShootVelocity.RotatedBy(Main.rand.NextFloat(-0.1f, 0.1f))
                    , AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            }

            _ = UpdateConsumeAmmo();
        }
    }
}

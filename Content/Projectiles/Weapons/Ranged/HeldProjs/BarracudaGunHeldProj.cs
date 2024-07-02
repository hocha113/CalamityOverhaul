using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Terraria;
using Terraria.Audio;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class BarracudaGunHeldProj : BaseGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "BarracudaGun";
        public override int targetCayItem => ModContent.ItemType<BarracudaGun>();
        public override int targetCWRItem => ModContent.ItemType<BarracudaGunEcType>();
        bool canFire;
        public override void SetRangedProperty() {
            HandDistance = 25;
            HandDistanceY = 5;
            HandFireDistance = 25;
            HandFireDistanceY = -10;
            ShootPosNorlLengValue = -12;
            ShootPosToMouLengValue = 30;
            GunPressure = 0.3f;
            ControlForce = 0.05f;
            Recoil = 1.2f;
            RangeOfStress = 25;
        }

        public override void SetShootAttribute() {
            FiringDefaultSound = canFire = true;
            if (Owner.ownedProjectileCounts[ModContent.ProjectileType<BarracudaProj>()] > 0) {
                FiringDefaultSound = canFire = false;
            }
        }

        public override void FiringShoot() {
            if (!canFire) {
                return;
            }
            Projectile.NewProjectile(Source, GunShootPos, ShootVelocity.RotatedBy(-0.11f)
                , ModContent.ProjectileType<BarracudaProj>(), WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            Projectile.NewProjectile(Source, GunShootPos, ShootVelocity.RotatedBy(0.11f)
                , ModContent.ProjectileType<BarracudaProj>(), WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
        }
    }
}

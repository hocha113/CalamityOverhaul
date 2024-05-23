using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
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
            FiringDefaultSound = false;
        }

        public override void FiringIncident() {
            base.FiringIncident();
        }

        public override void FiringShoot() {
            if (Owner.ownedProjectileCounts[ModContent.ProjectileType<BarracudaProj>()] > 0) {
                return;
            }
            SoundEngine.PlaySound(Item.UseSound, Projectile.Center);
            Projectile.NewProjectile(Source, GunShootPos, ShootVelocity.RotatedBy(-0.11f), ModContent.ProjectileType<BarracudaProj>(), WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            Projectile.NewProjectile(Source, GunShootPos, ShootVelocity.RotatedBy(0.11f), ModContent.ProjectileType<BarracudaProj>(), WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
        }

        public override void FiringShootR() {
            base.FiringShootR();
        }
    }
}

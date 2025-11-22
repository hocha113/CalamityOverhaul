using CalamityOverhaul.Common;
using CalamityOverhaul.Content.RangedModify.Core;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class SlagMagnumHeld : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "SlagMagnum";
        public override void SetRangedProperty() {
            KreloadMaxTime = 90;
            FireTime = 20;
            HandIdleDistanceX = 16;
            HandIdleDistanceY = 2;
            HandFireDistanceX = 16;
            HandFireDistanceY = -2;
            ShootPosNorlLengValue = -7;
            ShootPosToMouLengValue = 10;
            RepeatedCartridgeChange = true;
            GunPressure = 0.3f;
            ControlForce = 0.05f;
            Recoil = 1.2f;
            RangeOfStress = 25;
            LoadingAmmoAnimation = LoadingAmmoAnimationEnum.Handgun;
            LoadingAA_Handgun.gunBodyY = -8;
            LoadingAA_Handgun.clipLocked = CWRSound.Gun_Magnum_ClipLocked;
            LoadingAA_Handgun.clipOut = CWRSound.Gun_Magnum_ClipOut;
        }

        public override void FiringShoot() {
            if (AmmoTypes == ProjectileID.Bullet) {
                AmmoTypes = Item.shoot;
            }
            Projectile.NewProjectile(Source, Projectile.Center, ShootVelocity, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
        }
    }
}

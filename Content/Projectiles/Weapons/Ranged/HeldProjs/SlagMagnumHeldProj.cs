using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class SlagMagnumHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "SlagMagnum";
        public override int targetCayItem => ModContent.ItemType<SlagMagnum>();
        public override int targetCWRItem => ModContent.ItemType<SlagMagnumEcType>();

        public override void SetRangedProperty() {
            kreloadMaxTime = 90;
            FireTime = 20;
            HandIdleDistanceX = 15;
            HandIdleDistanceY = 5;
            HandFireDistanceX = 15;
            HandFireDistanceY = -5;
            ShootPosNorlLengValue = -7;
            ShootPosToMouLengValue = 10;
            RepeatedCartridgeChange = true;
            GunPressure = 0.3f;
            ControlForce = 0.05f;
            Recoil = 2.2f;
            RangeOfStress = 25;
            LoadingAmmoAnimation = LoadingAmmoAnimationEnum.Handgun;
            LoadingAA_Handgun.gunBodyY = -12;
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

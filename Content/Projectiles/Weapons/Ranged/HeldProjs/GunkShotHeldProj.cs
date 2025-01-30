using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class GunkShotHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "GunkShot";
        public override int TargetID => ModContent.ItemType<GunkShot>();

        public override void SetRangedProperty() {
            kreloadMaxTime = 18;
            FireTime = 30;
            HandIdleDistanceX = 25;
            HandIdleDistanceY = 5;
            HandFireDistanceX = 25;
            HandFireDistanceY = -5;
            ShootPosNorlLengValue = -6;
            ShootPosToMouLengValue = 20;
            RepeatedCartridgeChange = true;
            GunPressure = 0.3f;
            ControlForce = 0.05f;
            Recoil = 2f;
            RangeOfStress = 25;
            EnableRecoilRetroEffect = true;
            RecoilRetroForceMagnitude = 7;
            LoadingAmmoAnimation = LoadingAmmoAnimationEnum.Shotgun;
            LoadingAA_Shotgun.Roting = 50;
            LoadingAA_Shotgun.gunBodyX = 3;
            LoadingAA_Shotgun.gunBodyY = 25;
            if (!MagazineSystem) {
                FireTime += kreloadMaxTime;
            }
        }

        public override void FiringShoot() {
            for (int i = 0; i < 5; i++) {
                int proj = Projectile.NewProjectile(Source, ShootPos, ShootVelocity.RotatedByRandom(0.1f)
                    , AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            }
        }
    }
}

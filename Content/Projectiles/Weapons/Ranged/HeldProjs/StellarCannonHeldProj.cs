using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class StellarCannonHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "StellarCannon";
        public override int targetCayItem => ModContent.ItemType<StellarCannon>();
        public override int targetCWRItem => ModContent.ItemType<StellarCannonEcType>();

        public override void SetRangedProperty() {
            kreloadMaxTime = 90;
            FireTime = 22;
            HandDistance = 25;
            HandDistanceY = 5;
            HandFireDistance = 25;
            HandFireDistanceY = -5;
            ShootPosNorlLengValue = -5;
            ShootPosToMouLengValue = 10;
            RepeatedCartridgeChange = true;
            GunPressure = 0.3f;
            ControlForce = 0.05f;
            Recoil = 1.2f;
            RangeOfStress = 25;
            AmmoTypeAffectedByMagazine = false;
            CanCreateSpawnGunDust = false;
            CanCreateCaseEjection = false;
            EnableRecoilRetroEffect = true;
            RecoilRetroForceMagnitude = 6;
            LoadingAmmoAnimation = LoadingAmmoAnimationEnum.Handgun;
            LoadingAA_Handgun.loadingAmmoStarg_y = -12;
        }

        public override void FiringShoot() {
            Projectile.NewProjectile(Source, GunShootPos, ShootVelocity.RotatedByRandom(0.06f)
                , Item.shoot, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
        }
    }
}

using CalamityOverhaul.Common;
using CalamityOverhaul.Content.RangedModify.Core;
using Terraria;
using Terraria.Audio;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class SepticSkewerHeld : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "SepticSkewer";
        public override void SetRangedProperty() {
            FireTime = 1;
            HandIdleDistanceX = 24;
            HandIdleDistanceY = 0;
            HandFireDistanceX = 24;
            HandFireDistanceY = -2;
            ShootPosNorlLengValue = -8;
            ShootPosToMouLengValue = -10;
            RepeatedCartridgeChange = true;
            GunPressure = 0;
            ControlForce = 0;
            Recoil = 0.2f;
            RangeOfStress = 25;
            AmmoTypeAffectedByMagazine = false;
            EnableRecoilRetroEffect = true;
            RecoilRetroForceMagnitude = 3;
            LoadingAA_None.gunBodyY = 0;
            CanCreateSpawnGunDust = false;
            CanCreateCaseEjection = false;
            InOwner_HandState_AlwaysSetInFireRoding = true;
            LoadingAmmoAnimation_AlwaysSetInFireRoding = true;
            LoadingAmmoAnimation = LoadingAmmoAnimationEnum.Handgun;
            LoadingAA_Handgun.clipOut = CWRSound.Gun_HandGun_ClipOut with { Volume = 0.75f };
            LoadingAA_Handgun.clipLocked = CWRSound.Gun_HandGun_ClipLocked with { Volume = 0.75f, Pitch = 0.2f };
            LoadingAA_Handgun.slideInShoot = CWRSound.Gun_HandGun_SlideInShoot with { Volume = 0.75f, Pitch = 0.2f };
            if (!MagazineSystem) {
                FireTime = 30;
            }
        }

        public override void HanderPlaySound() {
            if (BulletNum % 2 == 0) {
                SoundEngine.PlaySound(Item.UseSound, Projectile.Center);
            }
        }

        public override void FiringShoot() {
            Projectile.NewProjectile(Source, ShootPos, ShootVelocity.RotatedByRandom(0.1f)
                , Item.shoot, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
        }
    }
}

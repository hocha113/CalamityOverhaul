using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class SepticSkewerHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "SepticSkewer";
        public override int targetCayItem => ModContent.ItemType<SepticSkewer>();
        public override int targetCWRItem => ModContent.ItemType<SepticSkewerEcType>();

        public override void SetRangedProperty() {
            FireTime = 1;
            HandDistance = 24;
            HandDistanceY = 0;
            HandFireDistance = 24;
            HandFireDistanceY = -2;
            ShootPosNorlLengValue = -8;
            ShootPosToMouLengValue = -10;
            RepeatedCartridgeChange = true;
            GunPressure = 0;
            ControlForce = 0.02f;
            Recoil = 0.2f;
            RangeOfStress = 25;
            AmmoTypeAffectedByMagazine = false;
            EnableRecoilRetroEffect = true;
            RecoilRetroForceMagnitude = 3;
            LoadingAA_None.loadingAA_None_Y = 0;
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

        public override void FiringShoot() {
            GunPressure = 0;
            RecoilRetroForceMagnitude = 0;
            if (BulletNum == 7) {
                GunPressure = 0.3f;
                RecoilRetroForceMagnitude = 6;
            }

            Projectile.NewProjectile(Source, GunShootPos, ShootVelocity.RotatedByRandom(0.1f)
                , Item.shoot, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
        }
    }
}

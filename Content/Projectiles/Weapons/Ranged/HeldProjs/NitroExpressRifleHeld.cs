using CalamityOverhaul.Common;
using CalamityOverhaul.Content.RangedModify.Core;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class NitroExpressRifleHeld : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "NitroExpressRifle";
        public override void SetRangedProperty() {
            FireTime = 50;
            Recoil = 2.2f;
            KreloadMaxTime = 25;
            RangeOfStress = 25;
            GunPressure = 0.3f;
            ControlForce = 0.05f;
            HandIdleDistanceX = 25;
            HandIdleDistanceY = 5;
            HandFireDistanceX = 25;
            HandFireDistanceY = 0;
            ShootPosNorlLengValue = -8;
            ShootPosToMouLengValue = 20;
            ArmRotSengsBackNoFireOffset = -30;
            AutomaticPolishingEffect = true;
            RepeatedCartridgeChange = true;
            AmmoTypeAffectedByMagazine = false;
            EnableRecoilRetroEffect = true;
            RecoilRetroForceMagnitude = 6;
            LoadingAmmoAnimation = LoadingAmmoAnimationEnum.Shotgun;
            LoadingAA_Shotgun.pump = CWRSound.Gun_NERifle_SlideInt;
            ForcedConversionTargetAmmoFunc = () => AmmoTypes == ProjectileID.Bullet;
            ToTargetAmmo = CWRID.Proj_NitroShot;
            if (!MagazineSystem) {
                FireTime += 20;
            }
        }
    }
}

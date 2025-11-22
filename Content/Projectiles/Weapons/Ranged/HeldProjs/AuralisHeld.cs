using CalamityOverhaul.Content.RangedModify.Core;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class AuralisHeld : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Auralis";
        public override void SetRangedProperty() {
            KreloadMaxTime = 90;
            FireTime = 32;
            HandIdleDistanceX = 25;
            HandIdleDistanceY = 5;
            HandFireDistanceX = 25;
            HandFireDistanceY = -5;
            ShootPosNorlLengValue = -0;
            ShootPosToMouLengValue = 10;
            RepeatedCartridgeChange = true;
            GunPressure = 0.1f;
            ControlForce = 0.05f;
            Recoil = 1.2f;
            RangeOfStress = 25;
            EnableRecoilRetroEffect = true;
            RecoilRetroForceMagnitude = 13;
            ForcedConversionTargetAmmoFunc = () => true;
            ToTargetAmmo = CWRID.Proj_AuralisBullet;
            if (!MagazineSystem) {
                FireTime += 18;
                LazyRotationUpdate = true;
            }
        }
    }
}

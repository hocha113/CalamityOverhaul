using CalamityOverhaul.Content.RangedModify.Core;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class TheHiveHeld : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "TheHive";
        public override void SetRangedProperty() {
            KreloadMaxTime = 100;
            FireTime = 10;
            HandIdleDistanceX = 12;
            HandIdleDistanceY = 0;
            HandFireDistanceX = 8;
            HandFireDistanceY = -5;
            ShootPosNorlLengValue = 0;
            ShootPosToMouLengValue = 30;
            RepeatedCartridgeChange = true;
            GunPressure = 0.1f;
            ControlForce = 0.05f;
            Recoil = 1.5f;
            RangeOfStress = 25;
            AmmoTypeAffectedByMagazine = false;
            EnableRecoilRetroEffect = true;
            RecoilRetroForceMagnitude = 6;
            LoadingAA_None.Roting = 50;
            LoadingAA_None.gunBodyX = 3;
            LoadingAA_None.gunBodyY = 15;
        }
    }
}

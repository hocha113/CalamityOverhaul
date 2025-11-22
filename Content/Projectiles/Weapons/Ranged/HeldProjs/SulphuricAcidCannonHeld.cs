using CalamityOverhaul.Content.RangedModify.Core;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class SulphuricAcidCannonHeld : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "SulphuricAcidCannon";
        public override void SetRangedProperty() {
            KreloadMaxTime = 90;
            FireTime = 15;
            HandIdleDistanceX = 26;
            HandIdleDistanceY = 4;
            HandFireDistanceX = 26;
            HandFireDistanceY = -4;
            ShootPosNorlLengValue = -3;
            ShootPosToMouLengValue = 30;
            RepeatedCartridgeChange = true;
            EjectCasingProjSize = 1.6f;
            CanCreateSpawnGunDust = false;
            GunPressure = 0.2f;
            ControlForce = 0.05f;
            Recoil = 1f;
            RangeOfStress = 25;
            EnableRecoilRetroEffect = true;
            RecoilRetroForceMagnitude = 7;
            AmmoTypeAffectedByMagazine = false;
            ForcedConversionTargetAmmoFunc = () => true;
            ToTargetAmmo = CWRID.Proj_SulphuricBlast;
        }
    }
}

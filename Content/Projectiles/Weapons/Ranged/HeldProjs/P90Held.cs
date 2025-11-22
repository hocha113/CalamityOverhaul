using CalamityOverhaul.Content.RangedModify.Core;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class P90Held : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "P90";
        public override void SetRangedProperty() {
            KreloadMaxTime = 90;
            FireTime = 2;
            HandIdleDistanceY = 0;
            HandIdleDistanceX = HandFireDistanceX = 12;
            HandFireDistanceY = -6;
            ShootPosNorlLengValue = 0;
            ShootPosToMouLengValue = 0;
            RepeatedCartridgeChange = true;
            MustConsumeAmmunition = false;
            Recoil = GunPressure = ControlForce = 0;
            ForcedConversionTargetAmmoFunc = () => true;
            ToTargetAmmo = CWRID.Proj_P90Round;
            LoadingAA_None.Roting = 20;
            LoadingAA_None.gunBodyX = 3;
            LoadingAA_None.gunBodyY = 5;
            CanCreateSpawnGunDust = false;
        }
    }
}

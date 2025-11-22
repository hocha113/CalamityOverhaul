using CalamityOverhaul.Content.RangedModify.Core;
using Terraria;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class BlissfulBombardierHeld : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "BlissfulBombardier";
        public override void SetRangedProperty() {
            KreloadMaxTime = 130;
            FireTime = 12;
            HandIdleDistanceX = 24;
            HandIdleDistanceY = 2;
            HandFireDistanceX = 24;
            HandFireDistanceY = -2;
            ShootPosNorlLengValue = -10;
            ShootPosToMouLengValue = 30;
            RepeatedCartridgeChange = true;
            GunPressure = 0.15f;
            ControlForce = 0.03f;
            Recoil = 1.2f;
            RangeOfStress = 25;
            EnableRecoilRetroEffect = true;
            RecoilRetroForceMagnitude = 13;
            EjectCasingProjSize = 1.4f;
        }

        public override void FiringShoot() {
            Projectile.NewProjectile(Source, ShootPos, ShootVelocity, CWRID.Proj_
                , WeaponDamage, WeaponKnockback, Owner.whoAmI, AmmoTypes);
        }
    }
}

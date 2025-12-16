using CalamityOverhaul.Content.RangedModify.Core;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged
{
    internal class PestilentDefilerHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "PestilentDefiler";
        public override int TargetID => CWRID.Item_PestilentDefiler;
        public override void SetRangedProperty() {
            KreloadMaxTime = 90;
            FireTime = 20;
            HandIdleDistanceX = 25;
            HandIdleDistanceY = 5;
            HandFireDistanceX = 25;
            HandFireDistanceY = -4;
            ShootPosNorlLengValue = -6;
            ShootPosToMouLengValue = 10;
            RepeatedCartridgeChange = true;
            GunPressure = 0.2f;
            ControlForce = 0.05f;
            Recoil = 1.2f;
            RangeOfStress = 25;
            EnableRecoilRetroEffect = true;
            RecoilRetroForceMagnitude = 9;
        }

        public override void FiringShoot() {
            if (AmmoTypes == ProjectileID.Bullet) {
                AmmoTypes = CWRID.Proj_SicknessRound;
                WeaponDamage = (int)(WeaponDamage * 0.6f);
            }
            Projectile.NewProjectile(Source, ShootPos, ShootVelocity, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
        }
    }
}

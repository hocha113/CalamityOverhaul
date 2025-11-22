using CalamityOverhaul.Content.RangedModify.Core;
using Terraria;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class StormSurgeHeld : BaseGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "StormSurge";
        public override void SetRangedProperty() {
            HandIdleDistanceX = 15;
            HandIdleDistanceY = 3;
            HandFireDistanceX = 15;
            HandFireDistanceY = -5;
            ShootPosNorlLengValue = 0;
            ShootPosToMouLengValue = 10;
            GunPressure = 0.05f;
            ControlForce = 0.05f;
            Recoil = 1.5f;
            RangeOfStress = 25;
            CanCreateCaseEjection = CanCreateSpawnGunDust = false;

        }

        public override void FiringShoot() {
            Projectile.NewProjectile(Source, ShootPos, ShootVelocity
                , CWRID.Proj_StormSurgeTornado, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
        }
    }
}

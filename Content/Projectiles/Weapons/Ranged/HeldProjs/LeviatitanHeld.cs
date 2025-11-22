using CalamityOverhaul.Content.RangedModify.Core;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class LeviatitanHeld : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Leviatitan";
        public override void SetRangedProperty() {
            KreloadMaxTime = 100;
            FireTime = 9;
            ControlForce = 0.04f;
            GunPressure = 0.2f;
            Recoil = 0.8f;
            HandIdleDistanceX = 26;
            HandIdleDistanceY = 6;
            HandFireDistanceX = 26;
            HandFireDistanceY = -4;
            CanCreateSpawnGunDust = CanCreateCaseEjection = false;
            EnableRecoilRetroEffect = true;
            RecoilRetroForceMagnitude = 8;
        }

        public override void FiringShoot() {
            if (AmmoTypes == ProjectileID.Bullet) {
                AmmoTypes = CWRID.Proj_AquaBlastToxic;
                if (Main.rand.NextBool(2)) {
                    AmmoTypes = CWRID.Proj_AquaBlast;
                }
            }
            Projectile.NewProjectile(Source, Projectile.Center, ShootVelocity, AmmoTypes
                , WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
        }
    }
}

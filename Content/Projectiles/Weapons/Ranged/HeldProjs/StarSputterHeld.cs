using CalamityOverhaul.Content.RangedModify.Core;
using Terraria;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class StarSputterHeld : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "StarSputter";
        private int chargeIndex;
        public override void SetRangedProperty() {
            KreloadMaxTime = 90;
            FireTime = 5;
            HandIdleDistanceX = 25;
            HandIdleDistanceY = 5;
            HandFireDistanceX = 25;
            HandFireDistanceY = -10;
            ShootPosNorlLengValue = 0;
            ShootPosToMouLengValue = 10;
            RepeatedCartridgeChange = true;
            GunPressure = 0.1f;
            ControlForce = 0.05f;
            Recoil = 1.2f;
            RangeOfStress = 25;
            AmmoTypeAffectedByMagazine = false;
            EnableRecoilRetroEffect = true;
            CanCreateCaseEjection = false;
            CanCreateSpawnGunDust = false;
            RecoilRetroForceMagnitude = 6;
        }

        public override void SetShootAttribute() {
            FireTime = 5;
            if (fireIndex > 2) {
                FireTime = 30;
                chargeIndex++;
                fireIndex = 0;
            }
        }

        public override void FiringShoot() {

            if (chargeIndex > 3) {
                Projectile.NewProjectile(Source, ShootPos, ShootVelocity
                    , CWRID.Proj_SputterCometBig, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
                chargeIndex = 0;
            }
            int proj = Projectile.NewProjectile(Source, ShootPos, ShootVelocity
                , AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            Main.projectile[proj].usesLocalNPCImmunity = true;
            Main.projectile[proj].localNPCHitCooldown = -1;

        }

        public override void PostFiringShoot() => fireIndex++;
    }
}

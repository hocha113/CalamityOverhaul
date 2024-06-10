using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class StarSputterHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "StarSputter";
        public override int targetCayItem => ModContent.ItemType<StarSputter>();
        public override int targetCWRItem => ModContent.ItemType<StarSputterEcType>();

        private int fireIndex;
        private int chargeIndex;
        public override void SetRangedProperty() {
            kreloadMaxTime = 90;
            FireTime = 5;
            HandDistance = 25;
            HandDistanceY = 5;
            HandFireDistance = 25;
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
                Projectile.NewProjectile(Source, GunShootPos, ShootVelocity
                    , ModContent.ProjectileType<SputterCometBig>(), WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
                chargeIndex = 0;
            }
            int proj = Projectile.NewProjectile(Source, GunShootPos, ShootVelocity
                , AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            Main.projectile[proj].usesLocalNPCImmunity = true;
            Main.projectile[proj].localNPCHitCooldown = -1;
            
        }

        public override void PostFiringShoot() => fireIndex++;
    }
}

using CalamityOverhaul.Content.RangedModify.Core;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class PridefulHuntersPlanarRipperHeld : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "PridefulHuntersPlanarRipper";
        public override void SetRangedProperty() {
            KreloadMaxTime = 122;
            FireTime = 3;
            HandIdleDistanceX = 15;
            HandIdleDistanceY = 5;
            HandFireDistanceX = 15;
            HandFireDistanceY = -5;
            ShootPosNorlLengValue = -2;
            ShootPosToMouLengValue = 6;
            GunPressure = 0.1f;
            ControlForce = 0.05f;
            Recoil = 0.3f;
            RangeOfStress = 25;
            EnableRecoilRetroEffect = true;
            InOwner_HandState_AlwaysSetInFireRoding = true;
            RecoilRetroForceMagnitude = 4;
            SpwanGunDustData.splNum = 0.4f;
            fireIndex = 0;
        }

        public override void HanderCaseEjection() {
            if (fireIndex > 15) {
                for (int i = 0; i < 13; i++) {
                    CaseEjection();
                }
            }
            CaseEjection();
        }

        public override void FiringShoot() {
            if (AmmoTypes == ProjectileID.Bullet) {
                AmmoTypes = CWRID.Proj_PlanarRipperBolt;
            }
            FireTime = 3;
            GunPressure = 0.1f;
            ControlForce = 0.05f;
            Recoil = 0.3f;
            RecoilRetroForceMagnitude = 4;
            fireIndex++;
            if (fireIndex > 15) {
                FireTime = 13;
                GunPressure = 0.3f;
                ControlForce = 0.05f;
                Recoil = 1.3f;
                RecoilRetroForceMagnitude = 8;
                fireIndex = 0;
                SoundEngine.PlaySound("CalamityMod/Sounds/Item/ScorchedEarthShot3".GetSound() with { Pitch = 0.8f, Volume = 0.5f });
                for (int i = 0; i < 15; i++) {
                    UpdateMagazineContents();
                    int proj = Projectile.NewProjectile(Source, ShootPos, ShootVelocity.RotatedByRandom(0.12f)
                        , AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
                    Main.projectile[proj].usesLocalNPCImmunity = true;
                    Main.projectile[proj].localNPCHitCooldown = 10;
                }
            }

            Projectile.NewProjectile(Source, ShootPos, ShootVelocity, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
        }
    }
}

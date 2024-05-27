using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class PridefulHuntersPlanarRipperHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "PridefulHuntersPlanarRipper";
        public override int targetCayItem => ModContent.ItemType<PridefulHuntersPlanarRipper>();
        public override int targetCWRItem => ModContent.ItemType<PridefulHuntersPlanarRipperEcType>();

        private int fireIndex;
        public override void SetRangedProperty() {
            kreloadMaxTime = 122;
            FireTime = 3;
            HandDistance = 15;
            HandDistanceY = 5;
            HandFireDistance = 15;
            HandFireDistanceY = -5;
            ShootPosNorlLengValue = -2;
            ShootPosToMouLengValue = 6;
            GunPressure = 0.1f;
            ControlForce = 0.05f;
            Recoil = 0.3f;
            RangeOfStress = 25;
            EnableRecoilRetroEffect = true;
            RecoilRetroForceMagnitude = 4;
            fireIndex = 0;
        }

        public override void PreInOwnerUpdate() {
            LoadingAnimation(50, 3, 25);
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
            SpawnGunFireDust(GunShootPos, ShootVelocity);
            if (AmmoTypes == ProjectileID.Bullet) {
                AmmoTypes = ModContent.ProjectileType<PlanarRipperBolt>();
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
                SoundEngine.PlaySound(ScorchedEarthEcType.ShootSound with { Pitch = 0.8f, Volume = 0.5f });
                for (int i = 0; i < 13; i++) {
                    UpdateMagazineContents();
                    int proj = Projectile.NewProjectile(Source, GunShootPos, ShootVelocity.RotatedByRandom(0.12f), AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
                    Main.projectile[proj].usesLocalNPCImmunity = true;
                    Main.projectile[proj].localNPCHitCooldown = 10;
                }
            }

            Projectile.NewProjectile(Source, GunShootPos, ShootVelocity, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
        }
    }
}

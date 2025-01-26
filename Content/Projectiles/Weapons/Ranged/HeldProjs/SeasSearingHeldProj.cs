using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Terraria;
using Terraria.Audio;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class SeasSearingHeldProj : BaseGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "SeasSearing";
        public override int targetCayItem => ModContent.ItemType<SeasSearing>();
        public override int targetCWRItem => ModContent.ItemType<SeasSearingEcType>();
        private const int maxFireCount = 7;
        private int indexFire;
        private int noFireTime;
        public override void SetRangedProperty() {
            ControlForce = 0.05f;
            GunPressure = 0.12f;
            Recoil = 0.75f;
            ShootPosToMouLengValue = 10;
            CanRightClick = true;
            CanCreateCaseEjection = false;
            CanCreateSpawnGunDust = false;
        }

        public override void PostInOwnerUpdate() {
            if (noFireTime > 0) {
                ShootCoolingValue = 2;
                noFireTime--;
                if (noFireTime == 30) {
                    SoundEngine.PlaySound(CWRSound.Gun_HandGun_SlideInShoot
                        with { PitchRange = (-0.1f, 0.1f), Volume = 0.4f }, Projectile.Center);
                    for (int i = 0; i < maxFireCount; i++) {
                        CaseEjection();
                    }
                }
            }
        }

        public override void SetShootAttribute() {
            if (onFire) {
                Item.useTime = 3;
                AmmoTypes = ModContent.ProjectileType<SeasSearingBubble>();
            }
            else if (onFireR) {
                Item.useTime = 30;
                AmmoTypes = ModContent.ProjectileType<SeasSearingSecondary>();
            }
        }

        public override void FiringShoot() {
            if (noFireTime > 0) {
                return;
            }

            Projectile.NewProjectile(Source, ShootPos, ShootVelocity, AmmoTypes, WeaponDamage
                , WeaponKnockback, Owner.whoAmI, (indexFire == 1 || indexFire == 5) ? 1 : 0);

            _ = UpdateConsumeAmmo();
        }

        public override void PostFiringShoot() {
            if (onFire) {
                indexFire++;
                if (indexFire >= maxFireCount) {
                    indexFire = 0;
                    noFireTime += 45;
                }
            }
        }
    }
}

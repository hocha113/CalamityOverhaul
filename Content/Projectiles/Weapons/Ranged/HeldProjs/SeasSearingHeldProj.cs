using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using Terraria;
using Terraria.Audio;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class SeasSearingHeldProj : BaseGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "SeasSearing";
        public override int targetCayItem => ModContent.ItemType<CalamityMod.Items.Weapons.Ranged.SeasSearing>();
        public override int targetCWRItem => ModContent.ItemType<SeasSearingEcType>();
        private const int maxFireCount = 7;
        private int indexFire;
        private int noFireTime;
        public override void SetRangedProperty() {
            ControlForce = 0.05f;
            GunPressure = 0.12f;
            Recoil = 0.75f;
            CanRightClick = true;
            FiringDefaultSound = false;
        }

        public override void FiringIncident() {
            base.FiringIncident();
            if (noFireTime > 0) {
                noFireTime--;
                if (noFireTime == 30) {
                    SoundEngine.PlaySound(CWRSound.Gun_HandGun_SlideInShoot with { PitchRange = (-0.1f, 0.1f), Volume = 0.4f }, Projectile.Center);
                    for (int i = 0; i < maxFireCount; i++) {
                        CaseEjection();
                    }
                }
            }
        }

        public override void FiringShoot() {
            Item.useTime = 3;
            if (noFireTime > 0) {
                return;
            }
            SoundEngine.PlaySound(Item.UseSound, Projectile.Center);
            AmmoTypes = ModContent.ProjectileType<SeasSearingBubble>();
            Projectile.NewProjectile(Source, GunShootPos, ShootVelocity, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, (indexFire == 1 || indexFire == 5)? 1 : 0);
            indexFire++;
            if (indexFire >= maxFireCount) {
                indexFire = 0;
                noFireTime += 45;
            }
            _ = UpdateConsumeAmmo();
            _ = CreateRecoil();
        }

        public override void FiringShootR() {
            Item.useTime = 30;
            SoundEngine.PlaySound(Item.UseSound, Projectile.Center);
            AmmoTypes = ModContent.ProjectileType<SeasSearingSecondary>();
            base.FiringShootR();
        }
    }
}

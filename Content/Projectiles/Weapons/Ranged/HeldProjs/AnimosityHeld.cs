using CalamityOverhaul.Common;
using CalamityOverhaul.Content.RangedModify.Core;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class AnimosityHeld : BaseFeederGun, ICWRLoader
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Animosity";
        private static int btoole;
        void ICWRLoader.SetupData() => btoole = ModLoader.GetMod("CalamityMod").Find<ModProjectile>("AnimosityBullet").Type;
        public override void SetRangedProperty() {
            FireTime = 6;
            ControlForce = 0.03f;
            GunPressure = 0.1f;
            Recoil = 1.2f;
            HandIdleDistanceX = 27;
            HandIdleDistanceY = 3;
            HandFireDistanceX = 27;
            HandFireDistanceY = -3;
            CanRightClick = true;
            LoadingAmmoAnimation = LoadingAmmoAnimationEnum.Handgun;
            LoadingAA_Handgun.gunBodyY = -16;
            LoadingAA_Handgun.clipLocked = CWRSound.Gun_HandGun_ClipLocked with { Pitch = -0.25f };
        }

        public override void HanderPlaySound() {
            if (onFire) {
                SoundEngine.PlaySound(SoundID.Item38 with { Pitch = 0.5f, PitchVariance = 0.3f }, Projectile.Center);
            }
            else if (onFireR) {
                SoundEngine.PlaySound("CalamityMod/Sounds/Item/WulfrumBlunderbussFireAndReload".GetSound() with { MaxInstances = 6, PitchVariance = 0.25f }, Projectile.Center);
            }
        }

        public override void SetShootAttribute() {
            if (onFire) {
                FireTime = 6;
                GunPressure = 0.1f;
                Recoil = 0.8f;
                RangeOfStress = 15;
                EnableRecoilRetroEffect = false;
                if (AmmoTypes == ProjectileID.Bullet) {
                    AmmoTypes = ProjectileID.BulletHighVelocity;
                }
            }
            else if (onFireR) {
                FireTime = 50;
                GunPressure = 0.2f;
                Recoil = 3;
                RangeOfStress = 25;
                EnableRecoilRetroEffect = true;
                RecoilRetroForceMagnitude = 6;
            }

        }
        public override void FiringShoot() {
            for (int i = 0; i < 2; i++) {
                int proj = Projectile.NewProjectile(Source, Projectile.Center, ShootVelocity.RotatedBy(Main.rand.NextFloat(-0.1f, 0.1f))
                    , AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
                Main.projectile[proj].SetBrimstoneBullets(true);
            }
            if (fireIndex > 5) {
                Projectile.NewProjectile(Source, Projectile.Center, ShootVelocity
                    , btoole, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            }
        }

        public override void PostFiringShoot() {
            if (onFireR) {
                return;
            }
            if (++fireIndex > 6) {
                FireTime = 25;
                fireIndex = 0;
            }
        }

        public override void FiringShootR() {
            Projectile.NewProjectile(Source, Projectile.Center, Vector2.Zero
                , ModContent.ProjectileType<AnimosityOnSpan>(), WeaponDamage
                , WeaponKnockback, Owner.whoAmI, 0, Projectile.identity);
        }
    }
}

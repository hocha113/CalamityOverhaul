using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Sounds;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class AnimosityHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Animosity";
        public override int targetCayItem => ModContent.ItemType<Animosity>();
        public override int targetCWRItem => ModContent.ItemType<AnimosityEcType>();

        private int btoole = ProjectileID.None;
        private int fireIndex;
        public override void SetRangedProperty() {
            FireTime = 6;
            ControlForce = 0.03f;
            GunPressure = 0.1f;
            Recoil = 1.2f;
            HandDistance = 27;
            HandDistanceY = 3;
            HandFireDistance = 27;
            HandFireDistanceY = -10;
            CanRightClick = true;
            FiringDefaultSound = false;
            LoadingAmmoAnimation = LoadingAmmoAnimationEnum.Handgun;
            LoadingAA_Handgun.loadingAmmoStarg_y = -16;
            LoadingAA_Handgun.clipLocked = CWRSound.Gun_HandGun_ClipLocked with { Pitch = -0.25f };
            btoole = ModLoader.GetMod("CalamityMod").Find<ModProjectile>("AnimosityBullet").Type;
        }

        public override void FiringShoot() {
            FireTime = 6;
            GunPressure = 0.1f;
            Recoil = 1.2f;
            RangeOfStress = 5;
            if (AmmoTypes == ProjectileID.Bullet) {
                AmmoTypes = ProjectileID.BulletHighVelocity;
            }
            SoundEngine.PlaySound(SoundID.Item38 with { Pitch = 0.5f, PitchVariance = 0.3f }, Projectile.Center);
            SpawnGunFireDust(GunShootPos, ShootVelocity);
            for (int i = 0; i < 2; i++) {
                Projectile.NewProjectile(Source, Projectile.Center, ShootVelocity.RotatedBy(Main.rand.NextFloat(-0.1f, 0.1f))
                    , AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            }
            CaseEjection();
            if (++fireIndex > 6) {
                SoundEngine.PlaySound(CommonCalamitySounds.LargeWeaponFireSound with { Pitch = -0.2f, Volume = 0.7f }, Projectile.Center);
                Projectile.NewProjectile(Source, Projectile.Center, ShootVelocity, btoole, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
                FireTime = 15;
                fireIndex = 0;
            }
        }

        public override void FiringShootR() {
            FireTime = 50;
            GunPressure = 0.6f;
            Recoil = 5;
            RangeOfStress = 25;
            SoundEngine.PlaySound(Animosity.ShootAndReloadSound, Projectile.Center);
            Projectile.NewProjectile(Source, Projectile.Center, Vector2.Zero
                , ModContent.ProjectileType<AnimosityOnSpan>(), WeaponDamage, WeaponKnockback, Owner.whoAmI, 0, Projectile.whoAmI);
            CaseEjection();
        }
    }
}

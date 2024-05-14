using CalamityMod.Items.Weapons.Ranged;
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
        int fireIndex;
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
        }

        public override void FiringShoot() {
            FireTime = 6;
            GunPressure = 0.1f;
            Recoil = 1.2f;
            RangeOfStress = 5;
            if (AmmoTypes == ProjectileID.Bullet) {
                AmmoTypes = ProjectileID.BulletHighVelocity;
            }
            SoundEngine.PlaySound(Animosity.ShootAndReloadSound, Projectile.Center);
            SpawnGunFireDust(GunShootPos, ShootVelocity);
            for (int i = 0; i < 2; i++) {
                Projectile.NewProjectile(Source, Projectile.Center, ShootVelocity.RotatedBy(Main.rand.NextFloat(-0.1f, 0.1f))
                    , AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            }
            CaseEjection();
            if (++fireIndex > 6) {
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

using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Particles;
using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Content.RangedModify.Core;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class SvantechnicalHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Svantechnical";
        public override int TargetID => ModContent.ItemType<Svantechnical>();
        int useAnimation;
        public override void SetRangedProperty() {
            kreloadMaxTime = 90;
            FireTime = 1;
            HandIdleDistanceX = 24;
            HandIdleDistanceY = 4;
            HandFireDistanceX = 24;
            HandFireDistanceY = -4;
            ShootPosNorlLengValue = 0;
            ShootPosToMouLengValue = 30;
            RepeatedCartridgeChange = true;
            GunPressure = 0;
            ControlForce = 0;
            Recoil = 1.2f;
            RangeOfStress = 25;
            CanCreateSpawnGunDust = false;
            CanRightClick = true;
        }

        public override void InitializeGun() => useAnimation = Item.useAnimation;
        public override void HanderPlaySound() {
            useAnimation -= FireTime;
            if (useAnimation <= 0) {
                SoundEngine.PlaySound(Item.UseSound, Projectile.Center);
                useAnimation = Item.useAnimation;
            }
        }

        public override void FiringShoot() {
            float sine = (float)Math.Sin(fireIndex * 0.175f / MathHelper.Pi) * 4f;
            float sine2 = (float)Math.Sin(fireIndex * 0.275f / MathHelper.Pi) * 2f;
            if (++fireIndex % 2 == 0) {
                Vector2 helixVel1 = (ShootVelocity * Main.rand.NextFloat(0.9f, 1.1f)).RotatedBy(MathHelper.ToRadians(sine));
                Vector2 helixVel2 = (ShootVelocity * Main.rand.NextFloat(0.9f, 1.1f)).RotatedBy(MathHelper.ToRadians(-sine));
                Vector2 helixVel3 = (ShootVelocity * Main.rand.NextFloat(0.9f, 1.1f)).RotatedBy(MathHelper.ToRadians(sine2));
                Projectile.NewProjectile(Source, ShootPos, helixVel1, ModContent.ProjectileType<ChargedBlast>(), WeaponDamage, WeaponKnockback, Owner.whoAmI, 0f, 0, 2f);
                Projectile.NewProjectile(Source, ShootPos, helixVel2, ModContent.ProjectileType<ChargedBlast>(), WeaponDamage, WeaponKnockback, Owner.whoAmI, 0f, 0, 4f);
                Projectile.NewProjectile(Source, ShootPos, helixVel3, ModContent.ProjectileType<ChargedBlast>(), WeaponDamage, WeaponKnockback, Owner.whoAmI, 0f, 0, 3f);
            }
            else {
                Particle spark2 = new LineParticle(ShootPos + Main.rand.NextVector2Circular(6, 6), (ShootVelocity * 4).RotatedByRandom(0.35f) * Main.rand.NextFloat(0.8f, 1.2f), false, Main.rand.Next(15, 25 + 1), Main.rand.NextFloat(1.5f, 2f), Main.rand.NextBool() ? Color.MediumOrchid : Color.DarkViolet);
                GeneralParticleHandler.SpawnParticle(spark2);
            }
        }
        public override void FiringShootR() {
            float sine = (float)Math.Sin(fireIndex * 0.175f / MathHelper.Pi) * 4f;
            float sine2 = (float)Math.Sin(fireIndex * 0.275f / MathHelper.Pi) * 2f;
            if (++fireIndex % 2 == 0) {
                Vector2 helixVel1 = (ShootVelocity * Main.rand.NextFloat(0.9f, 1.1f)).RotatedBy(MathHelper.ToRadians(sine));
                Vector2 helixVel2 = (ShootVelocity * Main.rand.NextFloat(0.9f, 1.1f)).RotatedBy(MathHelper.ToRadians(-sine));
                Vector2 helixVel3 = (ShootVelocity * Main.rand.NextFloat(0.9f, 1.1f)).RotatedBy(MathHelper.ToRadians(sine2));
                Projectile.NewProjectile(Source, ShootPos, helixVel1, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0f, 0, 2f);
                Projectile.NewProjectile(Source, ShootPos, helixVel2, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0f, 0, 4f);
                Projectile.NewProjectile(Source, ShootPos, helixVel3, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0f, 0, 3f);
            }
        }
    }
}

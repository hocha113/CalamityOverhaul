using CalamityMod;
using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Projectiles.Rogue;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla;
using CalamityOverhaul.Content.RangedModify.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class SpectralstormCannonHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "SpectralstormCannon";
        public override int TargetID => ModContent.ItemType<SpectralstormCannon>();
        public override void SetRangedProperty() {
            kreloadMaxTime = 90;
            FireTime = 20;
            HandIdleDistanceX = 25;
            HandIdleDistanceY = 5;
            HandFireDistanceX = 25;
            HandFireDistanceY = -10;
            ShootPosNorlLengValue = 2;
            ShootPosToMouLengValue = 20;
            RepeatedCartridgeChange = true;
            GunPressure = 0.1f;
            ControlForce = 0.05f;
            Recoil = 1.2f;
            RangeOfStress = 25;
            EnableRecoilRetroEffect = true;
            RecoilRetroForceMagnitude = 6;
            CanCreateSpawnGunDust = false;
        }

        public override void PostInOwnerUpdate() {
            if (!IsKreload || ShootCoolingValue > 0) {
                return;
            }

            int dustType = FlareGunHeldProj.GetFlareDustID(this);
            Vector2 projRotTo = Projectile.rotation.ToRotationVector2() * 13 + Owner.velocity;
            int dust = Dust.NewDust(ShootPos, 1, 1, dustType, projRotTo.X, projRotTo.Y);
            Main.dust[dust].noGravity = true;
            Main.dust[dust].scale = Main.rand.NextFloat(1, 1.6f);
        }

        public override void FiringShoot() {
            if (fireIndex > 25) {
                FireTime = 20;
                fireIndex = 0;
            }
            for (int index = 0; index < 3; ++index) {
                Vector2 velocity = ShootVelocity;
                velocity.X += Main.rand.Next(-40, 41) * 0.05f;
                velocity.Y += Main.rand.Next(-40, 41) * 0.05f;
                int proj = Projectile.NewProjectile(Source, ShootPos, velocity, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
                if (proj.WithinBounds(Main.maxProjectiles)) {
                    Main.projectile[proj].timeLeft = 150;
                    Main.projectile[proj].DamageType = DamageClass.Ranged;
                }
            }
            for (int index = 0; index < 3; ++index) {
                Vector2 velocity = ShootVelocity;
                velocity *= Main.rand.NextFloat(1, 1.1f);
                velocity.X += Main.rand.Next(-20, 21) * 0.05f;
                velocity.Y += Main.rand.Next(-20, 21) * 0.05f;
                int soul = Projectile.NewProjectile(Source, ShootPos, velocity, ModContent.ProjectileType<LostSoulFriendly>(), WeaponDamage, WeaponKnockback, Owner.whoAmI, 2f, 0f);
                if (soul.WithinBounds(Main.maxProjectiles)) {
                    Main.projectile[soul].timeLeft = 600;
                    Main.projectile[soul].DamageType = DamageClass.Ranged;
                    Main.projectile[soul].frame = Main.rand.Next(4);
                }
            }
            FireTime--;
            if (FireTime < 5) {
                FireTime = 5;
            }
            fireIndex++;
        }
    }
}

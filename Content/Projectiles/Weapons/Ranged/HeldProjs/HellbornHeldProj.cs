using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class HellbornHeldProj : BaseFeederGun
    {
        public override bool? CanDamage() {
            return (onFire || onFireR) && IsKreload ? null : base.CanDamage();
        }

        public static void HitFunc(Player player, NPC target) {
            player.ApplyDamageToNPC(target, player.GetShootState().WeaponDamage, 0f, 0, false);
            float firstDustScale = 3.4f;
            float secondDustScale = 1.6f;
            float thirdDustScale = 4f;
            Vector2 dustRotation = (target.rotation - MathHelper.PiOver2).ToRotationVector2();
            Vector2 dustVelocity = dustRotation * target.velocity.Length();
            _ = SoundEngine.PlaySound(SoundID.Item14, target.Center);
            for (int i = 0; i < 80; i++) {
                int contactDust = Dust.NewDust(new Vector2(target.position.X, target.position.Y), target.width, target.height, DustID.InfernoFork, 0f, 0f, 200, default, firstDustScale);
                Dust dust = Main.dust[contactDust];
                dust.position = target.Center + (Vector2.UnitY.RotatedByRandom(MathHelper.Pi) * (float)Main.rand.NextDouble() * target.width / 2f);
                dust.noGravity = true;
                dust.velocity.Y -= 6f;
                dust.velocity *= 3f;
                dust.velocity += dustVelocity * Main.rand.NextFloat();
                _ = Dust.NewDust(new Vector2(target.position.X, target.position.Y), target.width, target.height, DustID.InfernoFork, 0f, 0f, 100, default, secondDustScale);
                dust.position = target.Center + (Vector2.UnitY.RotatedByRandom(MathHelper.Pi) * (float)Main.rand.NextDouble() * target.width / 2f);
                dust.velocity.Y -= 6f;
                dust.velocity *= 2f;
                dust.noGravity = true;
                dust.fadeIn = 1f;
                dust.color = Color.Crimson * 0.5f;
                dust.velocity += dustVelocity * Main.rand.NextFloat();
            }
            for (int j = 0; j < 40; j++) {
                int contactDust2 = Dust.NewDust(new Vector2(target.position.X, target.position.Y), target.width, target.height, DustID.InfernoFork, 0f, 0f, 0, default, thirdDustScale);
                Dust dust = Main.dust[contactDust2];
                dust.position = target.Center + (Vector2.UnitX.RotatedByRandom(MathHelper.Pi).RotatedBy(target.velocity.ToRotation()) * target.width / 3f);
                dust.noGravity = true;
                dust.velocity.Y -= 6f;
                dust.velocity *= 0.5f;
                dust.velocity += dustVelocity * (0.6f + (0.6f * Main.rand.NextFloat()));
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) => HitFunc(Owner, target);

        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Hellborn";
        public override int targetCayItem => ModContent.ItemType<Hellborn>();
        public override int targetCWRItem => ModContent.ItemType<HellbornEcType>();
        public override void SetRangedProperty() {
            FireTime = 20;
            ControlForce = 0.05f;
            GunPressure = 0.2f;
            Recoil = 1;
            HandIdleDistanceX = 25;
            HandIdleDistanceY = 5;
            HandFireDistanceX = 20;
            HandFireDistanceY = -4;
            ShootPosNorlLengValue = -3;
            ShootPosToMouLengValue = 2;
            CanRightClick = true;
        }

        public override void SetShootAttribute() {
            if (onFireR) {
                EjectCasingProjSize = 1;
                FireTime = 7;
                Recoil = 0.5f;
                GunPressure = 0.1f;
                EnableRecoilRetroEffect = true;
                SpwanGunDustMngsData.splNum = 0.5f;
                return;
            }
            EjectCasingProjSize = 1.8f;
            FireTime = 20;
            Recoil = 1;
            GunPressure = 0.2f;
            EnableRecoilRetroEffect = false;
            SpwanGunDustMngsData.splNum = 1f;
        }

        public override void FiringShoot() {
            for (int i = 0; i < 4; i++) {
                _ = Projectile.NewProjectile(Source, GunShootPos,
                    ShootVelocity.RotatedByRandom(0.08f) * Main.rand.NextFloat(0.7f, 1.1f)
                    , ModContent.ProjectileType<RealmRavagerBullet>(), WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            }
        }

        public override void FiringShootR() {
            _ = Projectile.NewProjectile(Source, GunShootPos, ShootVelocity
                , AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
        }
    }
}

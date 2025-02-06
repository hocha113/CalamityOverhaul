using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.RangedModify.Core;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class OnyxiaHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Onyxia";
        public override int TargetID => ModContent.ItemType<Onyxia>();
        private int chargeIndex;
        private const int maxfireD = 15;
        private const int minfireD = 6;
        public override void SetRangedProperty() {
            kreloadMaxTime = 150;
            FireTime = maxfireD;
            HandIdleDistanceX = 25;
            HandIdleDistanceY = 5;
            HandFireDistanceX = 25;
            HandFireDistanceY = -5;
            ShootPosNorlLengValue = 0;
            ShootPosToMouLengValue = 10;
            RepeatedCartridgeChange = true;
            GunPressure = 0.1f;
            ControlForce = 0.05f;
            Recoil = 0.5f;
            RangeOfStress = 25;
            EnableRecoilRetroEffect = true;
            RecoilRetroForceMagnitude = 6;
        }

        public override void PostInOwnerUpdate() {
            if (!onFire) {
                FireTime = maxfireD;
                fireIndex = 0;
                chargeIndex = 0;
            }
        }

        public override void FiringShoot() {
            int shardDamage = (int)(2.45f * WeaponDamage);
            float shardKB = 2f * WeaponKnockback;
            float randomMode = 0.25f - fireIndex * 0.03f;
            if (randomMode < 0) {
                randomMode = 0;
            }
            Projectile shard = Projectile.NewProjectileDirect(Source, ShootPos, ShootVelocity.RotatedByRandom(randomMode), ProjectileID.BlackBolt, shardDamage, shardKB, Owner.whoAmI, 0f, 0f);
            shard.timeLeft = (int)(shard.timeLeft * 5.4f);
            if (fireIndex > 15) {
                shard.MaxUpdates *= 2;
            }

            for (int i = 0; i < 3; i++) {
                float randAngle = Main.rand.NextFloat(0.035f);
                float randVelMultiplier = Main.rand.NextFloat(0.92f, 1.08f);
                Vector2 ccwVelocity = ShootVelocity.RotatedBy(-randAngle) * randVelMultiplier;
                Vector2 cwVelocity = ShootVelocity.RotatedBy(randAngle) * randVelMultiplier;
                Projectile.NewProjectile(Source, ShootPos, ccwVelocity, AmmoTypes, WeaponDamage / 2, WeaponKnockback, Owner.whoAmI, 0f, 0f);
                Projectile.NewProjectile(Source, ShootPos, cwVelocity, AmmoTypes, WeaponDamage / 2, WeaponKnockback, Owner.whoAmI, 0f, 0f);
            }

        }

        public override void PostFiringShoot() {
            chargeIndex++;
            if (chargeIndex > 3) {
                FireTime--;
                if (FireTime < minfireD) {
                    FireTime = minfireD;
                }
                fireIndex++;
                chargeIndex = 0;
            }
            if (BulletNum <= 1) {
                FireTime = 10;
                fireIndex = 0;
                chargeIndex = 0;
            }
        }
    }
}

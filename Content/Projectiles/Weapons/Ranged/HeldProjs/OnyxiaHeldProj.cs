using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class OnyxiaHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Onyxia";
        public override int targetCayItem => ModContent.ItemType<Onyxia>();
        public override int targetCWRItem => ModContent.ItemType<OnyxiaEcType>();

        private int fireIndex;
        private int chargeIndex;
        private const int maxfireD = 15;
        private const int minfireD = 6;
        public override void SetRangedProperty() {
            kreloadMaxTime = 150;
            FireTime = maxfireD;
            HandDistance = 25;
            HandDistanceY = 5;
            HandFireDistance = 25;
            HandFireDistanceY = -5;
            ShootPosNorlLengValue = 0;
            ShootPosToMouLengValue = 10;
            RepeatedCartridgeChange = true;
            GunPressure = 0.15f;
            ControlForce = 0.05f;
            Recoil = 0.5f;
            RangeOfStress = 25;
            EnableRecoilRetroEffect = true;
            RecoilRetroForceMagnitude = 6;
        }

        public override void PreInOwnerUpdate() {
            LoadingAnimation(50, 3, 25);
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
            Projectile shard = Projectile.NewProjectileDirect(Source, GunShootPos, ShootVelocity.RotatedByRandom(randomMode), ProjectileID.BlackBolt, shardDamage, shardKB, Owner.whoAmI, 0f, 0f);
            shard.timeLeft = (int)(shard.timeLeft * 5.4f);
            if (fireIndex > 15) {
                shard.MaxUpdates *= 2;
            }

            for (int i = 0; i < 3; i++) {
                float randAngle = Main.rand.NextFloat(0.035f);
                float randVelMultiplier = Main.rand.NextFloat(0.92f, 1.08f);
                Vector2 ccwVelocity = ShootVelocity.RotatedBy(-randAngle) * randVelMultiplier;
                Vector2 cwVelocity = ShootVelocity.RotatedBy(randAngle) * randVelMultiplier;
                Projectile.NewProjectile(Source, GunShootPos, ccwVelocity, AmmoTypes, WeaponDamage / 2, WeaponKnockback, Owner.whoAmI, 0f, 0f);
                Projectile.NewProjectile(Source, GunShootPos, cwVelocity, AmmoTypes, WeaponDamage / 2, WeaponKnockback, Owner.whoAmI, 0f, 0f);
            }
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

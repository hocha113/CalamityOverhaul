using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using Microsoft.Xna.Framework;
using Terraria.ModLoader;
using Terraria;
using Mono.Cecil;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class OnyxiaHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Onyxia";
        public override int targetCayItem => ModContent.ItemType<Onyxia>();
        public override int targetCWRItem => ModContent.ItemType<OnyxiaEcType>();
        int fireIndex;
        int chargeIndex;
        public override void SetRangedProperty() {
            kreloadMaxTime = 150;
            FireTime = 10;
            HandDistance = 25;
            HandDistanceY = 5;
            HandFireDistance = 25;
            HandFireDistanceY = -10;
            ShootPosNorlLengValue = 0;
            ShootPosToMouLengValue = 10;
            RepeatedCartridgeChange = true;
            GunPressure = 0.15f;
            ControlForce = 0.05f;
            Recoil = 0.5f;
            RangeOfStress = 25;
            AmmoTypeAffectedByMagazine = false;
            EnableRecoilRetroEffect = true;
            RecoilRetroForceMagnitude = 6;
        }

        public override void PreInOwnerUpdate() {
            if (kreloadTimeValue > 0) {//设置一个特殊的装弹动作，调整转动角度和中心点，让枪身看起来上抬
                Owner.direction = ToMouse.X > 0 ? 1 : -1;//为了防止抽搐，这里额外设置一次玩家朝向
                FeederOffsetRot = -MathHelper.ToRadians(50) * DirSign;
                FeederOffsetPos = new Vector2(DirSign * -3, -25);
            }
        }

        public override void PostInOwnerUpdate() {
            if (!onFire) {
                FireTime = 10;
                fireIndex = 0;
                chargeIndex = 0;
            }
        }

        public override void FiringShoot() {
            SpawnGunFireDust(GunShootPos, ShootVelocity);
            int shardDamage = (int)(1.45f * WeaponKnockback);
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
                Projectile.NewProjectile(Source, GunShootPos, ccwVelocity, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0f, 0f);
                Projectile.NewProjectile(Source, GunShootPos, cwVelocity, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0f, 0f);
            }
            chargeIndex++;
            if (chargeIndex > 3) {
                FireTime--;
                if (FireTime < 4) {
                    FireTime = 4;
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

        public override void FiringShootR() {
            base.FiringShootR();
        }

        public override void PostFiringShoot() {
            base.PostFiringShoot();
        }
    }
}

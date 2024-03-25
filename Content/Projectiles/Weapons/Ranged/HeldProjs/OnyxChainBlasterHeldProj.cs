using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using Microsoft.Xna.Framework;
using Terraria.ModLoader;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class OnyxChainBlasterHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "OnyxChainBlaster";
        public override int targetCayItem => ModContent.ItemType<OnyxChainBlaster>();
        public override int targetCWRItem => ModContent.ItemType<OnyxChainBlasterEcType>();

        public override void SetRangedProperty() {
            kreloadMaxTime = 160;
            FireTime = 6;
            HandDistance = 22;
            HandDistanceY = 5;
            HandFireDistance = 22;
            HandFireDistanceY = -3;
            ShootPosNorlLengValue = -0;
            ShootPosToMouLengValue = 10;
            RepeatedCartridgeChange = true;
            MustConsumeAmmunition = false;
            GunPressure = 0.1f;
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
            base.PostInOwnerUpdate();
        }

        public override void FiringShoot() {
            SpawnGunFireDust(GunShootPos, ShootVelocity);
            int shardDamage = (int)(1.05f * WeaponKnockback);
            float shardKB = 1f * WeaponKnockback;
            Projectile shard = Projectile.NewProjectileDirect(Source, GunShootPos, ShootVelocity, ProjectileID.BlackBolt, shardDamage, shardKB, Owner.whoAmI, 0f, 0f);
            shard.timeLeft = (int)(shard.timeLeft * 2.4f);
            shard.MaxUpdates *= 2;

            for (int i = 0; i < 2; i++) {
                float randAngle = Main.rand.NextFloat(0.015f);
                float randVelMultiplier = Main.rand.NextFloat(0.72f, 1.08f);
                Vector2 ccwVelocity = ShootVelocity.RotatedBy(-randAngle) * randVelMultiplier;
                Vector2 cwVelocity = ShootVelocity.RotatedBy(randAngle) * randVelMultiplier;
                Projectile.NewProjectile(Source, GunShootPos, ccwVelocity, AmmoTypes, WeaponDamage / 2, WeaponKnockback, Owner.whoAmI, 0f, 0f);
                Projectile.NewProjectile(Source, GunShootPos, cwVelocity, AmmoTypes, WeaponDamage / 2, WeaponKnockback, Owner.whoAmI, 0f, 0f);
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

using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class OnyxChainBlasterHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "OnyxChainBlaster";
        public override int targetCayItem => ModContent.ItemType<OnyxChainBlaster>();
        public override int targetCWRItem => ModContent.ItemType<OnyxChainBlasterEcType>();

        public override void SetRangedProperty() {
            kreloadMaxTime = 120;
            FireTime = 6;
            HandIdleDistanceX = 22;
            HandIdleDistanceY = 5;
            HandFireDistanceX = 22;
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

        public override void FiringShoot() {
            int shardDamage = (int)(1.25f * WeaponKnockback);
            float shardKB = 1f * WeaponKnockback;
            Projectile shard = Projectile.NewProjectileDirect(Source, ShootPos, ShootVelocity, ProjectileID.BlackBolt, shardDamage, shardKB, Owner.whoAmI, 0f, 0f);
            shard.timeLeft = (int)(shard.timeLeft * 2.4f);
            shard.MaxUpdates *= 2;

            for (int i = 0; i < 2; i++) {
                float randAngle = Main.rand.NextFloat(0.015f);
                float randVelMultiplier = Main.rand.NextFloat(0.72f, 1.08f);
                Vector2 ccwVelocity = ShootVelocity.RotatedBy(-randAngle) * randVelMultiplier;
                Vector2 cwVelocity = ShootVelocity.RotatedBy(randAngle) * randVelMultiplier;
                Projectile.NewProjectile(Source, ShootPos, ccwVelocity, AmmoTypes, (int)(WeaponDamage * 0.6f), WeaponKnockback, Owner.whoAmI, 0f, 0f);
                Projectile.NewProjectile(Source, ShootPos, cwVelocity, AmmoTypes, (int)(WeaponDamage * 0.6f), WeaponKnockback, Owner.whoAmI, 0f, 0f);
            }
        }
    }
}

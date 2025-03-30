using CalamityOverhaul.Content.DamageModify;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeavenfallLongbowProj
{
    internal class HeavenRainbowImpact : ModProjectile
    {
        public const int Lifetime = 45;
        private Color ChromaColor => VaultUtils.MultiStepColorLerp(Projectile.timeLeft % 15 / 15f, HeavenfallLongbow.rainbowColors);
        public override string Texture => CWRConstant.Placeholder;
        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 10000;
        public override void SetDefaults() {
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.alpha = 255;
            Projectile.penetrate = -1;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = EndlessDamageClass.Instance;
            Projectile.MaxUpdates = 5;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = Projectile.MaxUpdates * 6;
            Projectile.timeLeft = Projectile.MaxUpdates * Lifetime;
        }

        public override void AI() {
            if (VaultUtils.isServer) {
                return;
            }

            Lighting.AddLight(Projectile.Center, Color.White.ToVector3());

            Color outerSparkColor = ChromaColor;
            float scaleBoost = MathHelper.Clamp(Projectile.ai[1] * 0.005f, 0f, 2f);
            float outerSparkScale = 1.3f + scaleBoost;
            PRT_HeavenfallStar spark = new PRT_HeavenfallStar(Projectile.Center, Projectile.velocity, false, 27, outerSparkScale, outerSparkColor);
            PRTLoader.AddParticle(spark);

            Color innerSparkColor = VaultUtils.MultiStepColorLerp(Projectile.ai[1] % 30 / 30f, HeavenfallLongbow.rainbowColors);
            float innerSparkScale = 0.6f + scaleBoost;
            PRT_HeavenfallStar spark2 = new PRT_HeavenfallStar(Projectile.Center, Projectile.velocity, false, 27, innerSparkScale, innerSparkColor);
            PRTLoader.AddParticle(spark2);
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float point = 0;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), Projectile.Center
                , Projectile.Center + Projectile.velocity.UnitVector() * -1600, Projectile.width, ref point);
        }
    }
}

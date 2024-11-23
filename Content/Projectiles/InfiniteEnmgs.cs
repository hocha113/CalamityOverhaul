using CalamityMod;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged.Extras;
using CalamityOverhaul.Content.Particles;
using InnoVault.PRT;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles
{
    internal class InfiniteEnmgs : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
            Projectile.penetrate = -1;
            Projectile.MaxUpdates = 2;
            Projectile.timeLeft = 130;
            Projectile.tileCollide = false;
            Projectile.friendly = true;
            Projectile.hostile = false;
        }

        public override void AI() {
            Lighting.AddLight(Projectile.Center, Main.DiscoColor.ToVector3());
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
            NPC potentialTarget = Projectile.Center.ClosestNPCAt(1500f, true, true);
            if (potentialTarget != null) {
                Projectile.velocity = (Projectile.velocity * 29f + Projectile.SafeDirectionTo(potentialTarget.Center) * 21f) / 30f;
                Projectile.velocity *= 1.01f;
            }

            if (!VaultUtils.isServer) {
                for (int i = 0; i < 3; i++) {
                    Vector2 vector = Projectile.velocity * 1.05f;
                    float slp = Main.rand.NextFloat(0.5f, 0.9f);
                    PRTLoader.AddParticle(new PRT_HeavenStar(Projectile.Center, vector, Color.White
                        , VaultUtils.MultiStepColorLerp(Main.rand.NextFloat(), HeavenfallLongbow.rainbowColors), 0f, new Vector2(0.6f, 1f) * slp
                        , new Vector2(1.5f, 2.7f) * slp, 20 + Main.rand.Next(6), 0f, 3f, 0f, Main.rand.Next(7) * 2, Main.rand.NextFloat(-0.3f, 0.3f)));
                }
            }
        }
    }
}

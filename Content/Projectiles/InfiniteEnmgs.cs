using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged.Extras;
using CalamityOverhaul.Content.Particles;
using CalamityOverhaul.Content.Particles.Core;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles
{
    internal class InfiniteEnmgs : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;
        public override bool IsLoadingEnabled(Mod mod) {
            if (!CWRServerConfig.Instance.AddExtrasContent) {
                return false;
            }
            return base.IsLoadingEnabled(mod);
        }
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
            NPC target = Projectile.Center.FindClosestNPC(1300);
            if (target != null) {
                Projectile.ChasingBehavior2(target.Center, 1, 0.1f);
            }

            if (!CWRUtils.isServer) {
                for (int i = 0; i < 3; i++) {
                    Vector2 vector = Projectile.velocity * 1.05f;
                    float slp = Main.rand.NextFloat(0.5f, 0.9f);
                    CWRParticleHandler.AddParticle(new HeavenStarParticle(Projectile.Center, vector, Color.White
                        , CWRUtils.MultiStepColorLerp(Main.rand.NextFloat(), HeavenfallLongbow.rainbowColors), 0f, new Vector2(0.6f, 1f) * slp
                        , new Vector2(1.5f, 2.7f) * slp, 20 + Main.rand.Next(6), 0f, 3f, 0f, Main.rand.Next(7) * 2, Main.rand.NextFloat(-0.3f, 0.3f)));
                }               
            }
        }
    }
}

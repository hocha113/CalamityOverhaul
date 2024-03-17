using CalamityMod.Particles;
using CalamityOverhaul.Common;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Magic
{
    internal class AetherOrb : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;
        public ref float Time => ref Projectile.ai[0];
        public override void SetDefaults() {
            Projectile.width = 5;
            Projectile.height = 5;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.alpha = 255;
            Projectile.penetrate = 1;
            Projectile.MaxUpdates = 5;
            Projectile.timeLeft = 60 * Projectile.MaxUpdates;
        }

        public override void AI() {
            Color InnerColor = Color.Blue;
            Lighting.AddLight(Projectile.Center, InnerColor.ToVector3() * 0.2f);
            Player Owner = Main.player[Projectile.owner];
            float targetDist = Vector2.Distance(Owner.Center, Projectile.Center);

            if (Projectile.timeLeft % 3 == 0 && Time > 5f && targetDist < 1400f) {
                AltSparkParticle spark = new(Projectile.Center, Projectile.velocity * 0.05f, false, 6, 2.3f, Color.White);
                GeneralParticleHandler.SpawnParticle(spark);
            }

            if (Main.rand.NextBool(3) && Time > 5f && targetDist < 1400f) {
                Particle orb = new GenericBloom(Projectile.Center + Main.rand.NextVector2Circular(10, 10)
                    , Projectile.velocity * Main.rand.NextFloat(0.05f, 0.5f), Color.AliceBlue, Main.rand.NextFloat(0.2f, 0.45f), Main.rand.Next(9, 12), true, false);
                GeneralParticleHandler.SpawnParticle(orb);
            }

            if (Projectile.timeLeft % 3 == 0 && Time > 5f && targetDist < 1400f) {
                LineParticle spark2 = new(Projectile.Center, -Projectile.velocity * 0.05f, false, 6, 1.7f, Color.White);
                GeneralParticleHandler.SpawnParticle(spark2);
            }

            Time++;
        }

        public override void OnKill(int timeLeft) {
            if (Projectile.owner == Main.myPlayer) {
                for (int i = 0; i < 8; i++) {
                    Vector2 velocity = ((MathHelper.TwoPi * i / 8f) - (MathHelper.Pi / 8f)).ToRotationVector2() * 4f;
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, velocity
                        , ModContent.ProjectileType<AetherOrb2>(), Projectile.damage, Projectile.knockBack, Projectile.owner);
                }
            }
        }
    }
}

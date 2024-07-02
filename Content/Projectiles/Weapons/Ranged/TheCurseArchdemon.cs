using CalamityMod.Dusts;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged
{
    internal class TheCurseArchdemon : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;

        public override void SetDefaults() {
            Projectile.width = 62;
            Projectile.height = 62;
            Projectile.friendly = true;
            Projectile.alpha = 0;
            Projectile.penetrate = 1;
            Projectile.MaxUpdates = 3;
            Projectile.timeLeft = 180;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
        }

        public override void AI() {
            CWRUtils.ClockFrame(ref Projectile.frameCounter, 10, 4);
            Projectile.rotation = Projectile.velocity.ToRotation();
            SpanDust();

            //for (int i = 0; i < 10; i++)//生成这种粒子不是好主意
            //{
            //    Vector2 particleSpeed = Projectile.rotation.ToRotationVector2() * 38 * (i / 20f);
            //    Particle energyLeak = new SquishyLightParticle(Projectile.Center, particleSpeed
            //        , Main.rand.NextFloat(1.6f, 1.8f), Color.Blue, 60, 1, 1.5f, hueShift: 0.01f);
            //    GeneralParticleHandler.SpawnParticle(energyLeak);
            //}
        }

        public override void OnKill(int timeLeft) {
            Projectile.Explode();

            //for (int i = 0; i < 30; i++)
            //{
            //    Vector2 particleSpeed = Main.rand.NextVector2Unit() * Main.rand.Next(3, 7);
            //    Particle energyLeak = new SquishyLightParticle(Projectile.Center, particleSpeed
            //        , Main.rand.NextFloat(0.6f, 1.3f), Color.DarkRed, 60, 1, 1.5f, hueShift: 0.0f);
            //    GeneralParticleHandler.SpawnParticle(energyLeak);
            //}

            for (int k = 0; k < 32; k++) {
                Dust.NewDust(Projectile.position + Projectile.velocity, Projectile.width, Projectile.height
                    , (int)CalamityDusts.Brimstone, Projectile.oldVelocity.X * 0.15f, Projectile.oldVelocity.Y * 0.15f);
            }
        }

        public void SpanDust() {
            for (int i = 0; i < 2; i++) {
                int num154 = 14;
                int num155 = Dust.NewDust(new Vector2(Projectile.Center.X, Projectile.Center.Y), Projectile.width - num154 * 2, Projectile.height - num154 * 2, 60, 0f, 0f, 100, default, 1.6f);
                Main.dust[num155].noGravity = true;
                Main.dust[num155].velocity *= 0.1f;
                Main.dust[num155].velocity += Projectile.velocity * 0.5f;
            }
            if (Main.rand.NextBool(13)) {
                int num156 = 16;
                int num157 = Dust.NewDust(new Vector2(Projectile.Center.X, Projectile.Center.Y), Projectile.width - num156 * 2, Projectile.height - num156 * 2, 60, 0f, 0f, 100, default, 1f);
                Main.dust[num157].velocity *= 0.25f;
                Main.dust[num157].velocity += Projectile.velocity * 0.5f;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            //Texture2D mainValue = CWRUtils.GetT2DValue(Texture);
            //Main.EntitySpriteDraw(
            //    mainValue,
            //    Projectile.Center - Main.screenPosition,
            //    CWRUtils.GetRec(mainValue, Projectile.frameCounter, 5),
            //    lightColor * (Projectile.alpha / 255f),
            //    Projectile.rotation,
            //    CWRUtils.GetOrig(mainValue, 4),
            //    Projectile.scale,
            //    Projectile.velocity.X > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically
            //    );
            return false;
        }
    }
}

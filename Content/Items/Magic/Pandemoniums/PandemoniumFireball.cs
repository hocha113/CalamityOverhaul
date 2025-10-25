using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic.Pandemoniums
{
    ///<summary>
    ///混沌魔能火球 - 增强版
    ///</summary>
    internal class PandemoniumFireball : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;
        private bool exploded = false;
        private ref float DelayTimer => ref Projectile.ai[0];
        private bool initialized = false;
        private Vector2 targetVelocity;

        public override void SetDefaults() {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.hostile = false;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 240;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI() {
            if (!initialized && DelayTimer > 0) {
                Projectile.velocity = Vector2.Zero;
                DelayTimer--;

                // 延迟期间的充能效果
                if (Main.rand.NextBool(2)) {
                    Dust d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(30f, 30f),
                        DustID.Torch, Vector2.Zero, 100, Color.OrangeRed, 1.2f);
                    d.noGravity = true;
                }

                return;
            }

            if (!initialized) {
                initialized = true;
                targetVelocity = Projectile.velocity;
            }

            // 加速发射
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, targetVelocity.SafeNormalize(Vector2.Zero) * 15f, 0.1f);
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            // 增强拖尾效果
            for (int i = 0; i < 3; i++) {
                Vector2 offset = Projectile.velocity.SafeNormalize(Vector2.Zero) * -i * 8f;
                Color dustColor = Color.Lerp(Color.Red, Color.Orange, i / 3f);
                var d = Dust.NewDustPerfect(Projectile.Center + offset, DustID.AncientLight, Vector2.Zero, 100, dustColor, 2.0f);
                d.noGravity = true;
                d.velocity = Main.rand.NextVector2Circular(1.5f, 1.5f);
            }

            // 火焰粒子
            if (Main.rand.NextBool(1)) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Torch,
                    Main.rand.NextVector2Circular(2f, 2f), 100, default, 1.5f);
                d.noGravity = true;
            }

            Lighting.AddLight(Projectile.Center, 1.5f, 0.8f, 0.4f);
        }

        public override void OnKill(int timeLeft) {
            if (exploded) return;
            Explode();
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            Explode();
            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (exploded) return;
            Explode();
        }

        private void Explode() {
            if (exploded) return;
            exploded = true;

            Projectile.position = Projectile.Center;
            Projectile.width = Projectile.height = 300;
            Projectile.Center = Projectile.position;
            Projectile.Damage();

            SoundEngine.PlaySound(SoundID.Item74 with { Volume = 1.2f, Pitch = -0.5f }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.DD2_ExplosiveTrapExplode with { Volume = 0.8f, Pitch = -0.3f }, Projectile.Center);

            //强化爆炸粒子
            for (int i = 0; i < 80; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(15f, 15f);
                var d = Dust.NewDustPerfect(Projectile.Center, DustID.Torch, vel, 100,
                    new Color(255, Main.rand.Next(80, 150), 40), Main.rand.NextFloat(2.0f, 3.5f));
                d.noGravity = true;
            }

            for (int i = 0; i < 50; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(10f, 10f);
                var d = Dust.NewDustPerfect(Projectile.Center, DustID.Smoke, vel, 150,
                    Color.DarkGray, Main.rand.NextFloat(2.0f, 3.0f));
                d.noGravity = true;
            }

            // 火环效果
            for (int i = 0; i < 30; i++) {
                float angle = MathHelper.TwoPi * i / 30f;
                Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(8f, 12f);
                var d = Dust.NewDustPerfect(Projectile.Center, DustID.Torch, vel, 100, Color.OrangeRed, 2.0f);
                d.noGravity = true;
            }

            Projectile.active = false;
        }

        public override bool PreDraw(ref Color lightColor) {
            if (!initialized && DelayTimer > 0) return false;

            Texture2D glow = CWRAsset.SoftGlow.Value;
            float time = Main.GlobalTimeWrappedHourly;
            float pulse = (float)Math.Sin(time * 25f) * 0.5f + 0.5f;

            Color c1 = new Color(255, 240, 200, 0);
            Color c2 = new Color(255, 150, 60, 0);
            Color c3 = new Color(255, 80, 30, 0);
            Color c4 = new Color(180, 30, 20, 0);

            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            Main.spriteBatch.Draw(glow, drawPos, null, c4 * 0.6f, 0, glow.Size() / 2, Projectile.scale * 2.5f, 0, 0);
            Main.spriteBatch.Draw(glow, drawPos, null, c3 * 0.8f, time * 2f, glow.Size() / 2, Projectile.scale * 2.0f, 0, 0);
            Main.spriteBatch.Draw(glow, drawPos, null, c2, -time * 3f, glow.Size() / 2, Projectile.scale * (1.5f + pulse * 0.3f), 0, 0);
            Main.spriteBatch.Draw(glow, drawPos, null, c1, time * 4f, glow.Size() / 2, Projectile.scale * (1.0f + pulse * 0.5f), 0, 0);

            return false;
        }
    }
}

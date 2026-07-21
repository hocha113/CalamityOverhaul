using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Accessories.JusticeUnveileds
{
    /// <summary>命中十字标记</summary>
    internal class JusticeCrossMark : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private ref float TargetNPCID => ref Projectile.ai[0];
        private ref float Timer => ref Projectile.ai[1];
        private float rotation = 0f;
        private float pulsePhase = 0f;
        private float fadeProgress = 0f;

        private const int MarkDuration = 90;//帧
        private const float CrossSize = 50f;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 60;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = MarkDuration;
            Projectile.alpha = 0;
        }

        public override void AI() {
            int npcId = (int)TargetNPCID;
            if (npcId < 0 || npcId >= Main.maxNPCs) {
                Projectile.Kill();
                return;
            }

            NPC npc = Main.npc[npcId];
            if (!npc.active) {
                Projectile.Kill();
                return;
            }

            Timer++;
            pulsePhase += 0.15f;
            rotation += 0.08f;

            Projectile.Center = npc.Center;

            //末30帧淡出
            if (Timer > MarkDuration - 30) {
                fadeProgress = (Timer - (MarkDuration - 30)) / 30f;
            }

            float lightIntensity = (float)Math.Sin(pulsePhase) * 0.5f + 0.5f;
            Lighting.AddLight(Projectile.Center,
                1.2f * lightIntensity * (1f - fadeProgress),
                0.9f * lightIntensity * (1f - fadeProgress),
                0.3f * lightIntensity * (1f - fadeProgress));

            if (Main.rand.NextBool(8) && fadeProgress < 0.7f) {
                SpawnMarkParticle();
            }
        }

        private void SpawnMarkParticle() {
            float angle = Main.rand.NextFloat(MathHelper.TwoPi);
            Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(2f, 5f);

            Dust mark = Dust.NewDustPerfect(
                Projectile.Center + Main.rand.NextVector2Circular(15f, 15f),
                DustID.GoldCoin,
                velocity,
                0,
                default,
                Main.rand.NextFloat(1f, 1.5f)
            );
            mark.noGravity = true;
            mark.fadeIn = 0.8f;
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Texture2D glowTex = CWRAsset.StarTexture.Value;
            Texture2D pixelTex = VaultAsset.placeholder2.Value;

            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float alpha = 1f - fadeProgress;

            float pulse = (float)Math.Sin(pulsePhase * 1.5f) * 0.2f + 0.8f;

            for (int i = 0; i < 3; i++) {
                float ringScale = (1.8f + i * 0.4f) * pulse;
                float ringAlpha = (1f - i * 0.3f) * alpha * 0.4f;
                Color ringColor = Color.Lerp(Color.Gold, Color.Yellow, i / 3f) with { A = 0 };

                sb.Draw(
                    glowTex,
                    drawPos,
                    null,
                    ringColor * ringAlpha,
                    rotation + i * MathHelper.PiOver4,
                    glowTex.Size() / 2f,
                    ringScale,
                    SpriteEffects.None,
                    0f
                );
            }

            DrawCross(sb, drawPos, alpha, pulse);

            Color coreColor = Color.White with { A = 0 };
            sb.Draw(
                glowTex,
                drawPos,
                null,
                coreColor * alpha * 0.8f * pulse,
                rotation,
                glowTex.Size() / 2f,
                0.6f * pulse,
                SpriteEffects.None,
                0f
            );

            return false;
        }

        private static void DrawCross(SpriteBatch sb, Vector2 drawPos, float alpha, float pulse) {
            Texture2D pixelTex = VaultAsset.placeholder2.Value;

            for (int i = 0; i < 4; i++) {
                float angle = i * MathHelper.PiOver2;
                Vector2 direction = angle.ToRotationVector2();

                Color mainColor = Color.Lerp(Color.Gold, Color.Yellow, 0.3f) with { A = 0 };
                Vector2 lineScale = new Vector2(CrossSize * pulse, 4f);

                sb.Draw(
                    pixelTex,
                    drawPos + direction * 5f,
                    null,
                    mainColor * alpha * 0.9f,
                    angle,
                    Vector2.Zero,
                    lineScale,
                    SpriteEffects.None,
                    0f
                );

                Color glowColor = Color.White with { A = 0 };
                Vector2 glowScale = new Vector2(CrossSize * pulse * 0.8f, 6f);

                sb.Draw(
                    pixelTex,
                    drawPos + direction * 5f,
                    null,
                    glowColor * alpha * 0.5f * pulse,
                    angle,
                    Vector2.Zero,
                    glowScale,
                    SpriteEffects.None,
                    0f
                );
            }

            //对角辅助十字
            for (int i = 0; i < 4; i++) {
                float angle = i * MathHelper.PiOver2 + MathHelper.PiOver4;
                Vector2 direction = angle.ToRotationVector2();

                Color auxColor = Color.Gold with { A = 0 };
                Vector2 auxScale = new Vector2(CrossSize * 0.6f * pulse, 2f);

                sb.Draw(
                    pixelTex,
                    drawPos + direction * 3f,
                    null,
                    auxColor * alpha * 0.6f,
                    angle,
                    Vector2.Zero,
                    auxScale,
                    SpriteEffects.None,
                    0f
                );
            }
        }

        public override void OnKill(int timeLeft) {
            for (int i = 0; i < 12; i++) {
                float angle = MathHelper.TwoPi * i / 12f;
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(2f, 6f);

                Dust fade = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.GoldCoin,
                    velocity,
                    0,
                    default,
                    Main.rand.NextFloat(1.2f, 2f)
                );
                fade.noGravity = true;
                fade.fadeIn = 0.8f;
            }

            for (int i = 0; i < 8; i++) {
                Dust flash = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.Electric,
                    Main.rand.NextVector2Circular(4f, 4f),
                    0,
                    Color.Gold,
                    Main.rand.NextFloat(1f, 1.5f)
                );
                flash.noGravity = true;
            }

            SoundEngine.PlaySound(SoundID.Item29 with {
                Volume = 0.25f,
                Pitch = 0.4f
            }, Projectile.Center);
        }
    }
}

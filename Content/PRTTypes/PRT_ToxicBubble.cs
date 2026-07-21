using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    /// <summary>硫磺海上升气泡</summary>
    internal class PRT_ToxicBubble : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "DiffusionCircle5";

        private float popProgress;
        private float shimmerTimer;
        private float floatWobble;
        private Color coreColor;
        private Color rimColor;
        private bool isPopping;

        public override bool CanPool => true;
        public PRT_ToxicBubble() {
            coreColor = new Color(120, 220, 140, 120);
            rimColor = new Color(180, 240, 160, 200);
        }
        public PRT_ToxicBubble(Vector2 position, Vector2 velocity, float scale, int lifetime) {
            Position = position;
            Velocity = velocity;
            Scale = scale;
            Lifetime = lifetime;

            coreColor = Main.rand.NextBool()
                ? new Color(120, 220, 140, 120)
                : new Color(150, 200, 100, 140);
            rimColor = new Color(180, 240, 160, 200);

            floatWobble = Main.rand.NextFloat(MathHelper.TwoPi);
            shimmerTimer = Main.rand.NextFloat(MathHelper.TwoPi);
        }
        public PRT_ToxicBubble Configure(int lt) {
            Lifetime = lt;
            coreColor = Main.rand.NextBool() ? new Color(120, 220, 140, 120) : new Color(150, 200, 100, 140);
            rimColor = new Color(180, 240, 160, 200);
            floatWobble = Main.rand.NextFloat(MathHelper.TwoPi);
            shimmerTimer = Main.rand.NextFloat(MathHelper.TwoPi);
            return this;
        }
        public override void Reset() {
            base.Reset();
            popProgress = 0f;
            shimmerTimer = 0f;
            floatWobble = 0f;
            coreColor = new Color(120, 220, 140, 120);
            rimColor = new Color(180, 240, 160, 200);
            isPopping = false;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
            Opacity = 0f;
        }

        public override void AI() {
            if (Time < 10) {
                Opacity = Time / 10f;
            }
            //近水面破裂
            else if (LifetimeCompletion > 0.85f) {
                if (!isPopping) {
                    isPopping = true;
                    popProgress = 0f;
                }
                popProgress = (LifetimeCompletion - 0.85f) / 0.15f;
                Opacity = 1f - popProgress;
            }
            else {
                Opacity = 1f;
            }

            shimmerTimer += 0.15f;
            floatWobble += 0.08f;
            float wobbleX = MathF.Sin(floatWobble) * 0.12f;
            Velocity.X += wobbleX;

            Velocity.Y *= 0.985f;
            Velocity.X *= 0.95f;

            if (isPopping) {
                Scale *= 1.05f;

                if (Main.rand.NextBool(3)) {
                    Vector2 splashVel = Main.rand.NextVector2Circular(2f, 2f);
                    splashVel.Y -= 1f;

                    PRTLoader.NewParticle<PRT_AcidSplash>(Position, splashVel, Color.White, Main.rand.NextFloat(0.3f, 0.6f)).Configure(Main.rand.Next(20, 40));
                }
            }
            else {
                Scale *= 1f + MathF.Sin(shimmerTimer) * 0.002f;
            }

            Rotation += 0.01f;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D texture = PRTLoader.PRT_IDToTexture[ID];
            Vector2 drawPos = Position - Main.screenPosition;
            Vector2 origin = texture.Size() / 2f;

            Vector2 scaleVector = isPopping
                ? new Vector2(Scale * (1f + popProgress * 0.5f), Scale * (1f - popProgress * 0.3f))
                : new Vector2(Scale);

            float rimPulse = MathF.Sin(shimmerTimer * 1.5f) * 0.3f + 0.7f;
            spriteBatch.Draw(
                texture,
                drawPos,
                null,
                rimColor * Opacity * rimPulse,
                Rotation,
                origin,
                scaleVector * 1.2f,
                SpriteEffects.None,
                0f
            );

            spriteBatch.Draw(
                texture,
                drawPos,
                null,
                coreColor * Opacity,
                Rotation,
                origin,
                scaleVector,
                SpriteEffects.None,
                0f
            );

            float shimmer = MathF.Sin(shimmerTimer * 2f) * 0.5f + 0.5f;
            Vector2 shimmerOffset = new Vector2(
                MathF.Cos(shimmerTimer) * Scale * 0.2f,
                MathF.Sin(shimmerTimer * 0.7f) * Scale * 0.15f
            );

            spriteBatch.Draw(
                texture,
                drawPos + shimmerOffset,
                null,
                Color.White * Opacity * shimmer * 0.4f,
                Rotation * 0.5f,
                origin,
                scaleVector * 0.4f,
                SpriteEffects.None,
                0f
            );

            return false;
        }
    }
}

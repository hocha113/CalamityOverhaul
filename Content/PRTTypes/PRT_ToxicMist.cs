using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    /// <summary>毒雾</summary>
    internal class PRT_ToxicMist : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Smoke";

        [VaultLoaden("@CalamityMod/Particles/BloomCircle")]
        internal static Asset<Texture2D> BloomTex = null;

        private float rotationSpeed;
        private float hueShift;
        private float depthLayer; //0-1 深浅
        private Color mistColor;
        private int frameIndex;

        public override bool CanPool => true;
        public PRT_ToxicMist() {
            depthLayer = 0.5f;
            mistColor = new Color(160, 230, 170, 180);
        }
        public PRT_ToxicMist(Vector2 position, Vector2 velocity, float scale, int lifetime, float depth = 0.5f) {
            Position = position;
            Velocity = velocity;
            Scale = scale;
            Lifetime = lifetime;
            depthLayer = MathHelper.Clamp(depth, 0f, 1f);
            rotationSpeed = Main.rand.NextFloat(-0.01f, 0.01f);
            hueShift = Main.rand.NextFloat(-0.02f, 0.02f);

            mistColor = depth > 0.6f
                ? new Color(100, 160, 80) //前景亮
                : new Color(70, 130, 70); //背景暗

            frameIndex = Main.rand.Next(16);
        }
        public PRT_ToxicMist Configure(int lt, float depth = 0.5f) {
            Lifetime = lt;
            depthLayer = MathHelper.Clamp(depth, 0f, 1f);
            rotationSpeed = Main.rand.NextFloat(-0.01f, 0.01f);
            hueShift = Main.rand.NextFloat(-0.02f, 0.02f);
            mistColor = depth > 0.6f ? new Color(100, 160, 80) : new Color(70, 130, 70);
            frameIndex = Main.rand.Next(16);
            return this;
        }
        public override void Reset() {
            base.Reset();
            rotationSpeed = 0f;
            hueShift = 0f;
            depthLayer = 0.5f;
            mistColor = new Color(160, 230, 170, 180);
            frameIndex = 0;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
            Opacity = 0f;
        }

        public override void AI() {
            float fadeIn = Math.Min(Time / 30f, 1f);
            float fadeOut = 1f - (float)Math.Pow(LifetimeCompletion, 2);
            Opacity = fadeIn * fadeOut * (0.3f + depthLayer * 0.4f);

            if (LifetimeCompletion < 0.3f) {
                Scale *= 1.008f;
            }
            else {
                Scale *= 0.997f;
            }

            mistColor = Main.hslToRgb(
                (Main.rgbToHsl(mistColor).X + hueShift) % 1,
                Main.rgbToHsl(mistColor).Y,
                Main.rgbToHsl(mistColor).Z
            );

            Rotation += rotationSpeed * (Velocity.X > 0 ? 1f : -1f);

            Velocity *= 0.98f;
            Velocity.Y -= 0.02f * depthLayer;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            if (BloomTex == null || BloomTex.IsDisposed) {
                return false;
            }

            Texture2D smokeTexture = PRTLoader.PRT_IDToTexture[ID];
            Texture2D bloomTexture = BloomTex.Value;

            Vector2 drawPos = Position - Main.screenPosition;

            int frameX = frameIndex % 4;
            int frameY = frameIndex / 4;
            Rectangle frame = new Rectangle(frameX * 256, frameY * 256, 256, 256);
            Vector2 origin = frame.Size() / 2f;

            Color drawColor = mistColor * Opacity;

            float bloomScale = Scale * 0.2f * (1f + (1f - depthLayer) * 0.05f);
            spriteBatch.Draw(
                bloomTexture,
                drawPos,
                null,
                drawColor * 0.3f,
                Rotation * 0.5f,
                bloomTexture.Size() / 2f,
                bloomScale,
                SpriteEffects.None,
                0f
            );

            spriteBatch.Draw(
                smokeTexture,
                drawPos,
                frame,
                drawColor,
                Rotation,
                origin,
                Scale,
                SpriteEffects.None,
                0f
            );

            if (depthLayer > 0.5f) {
                float glowIntensity = (depthLayer - 0.5f) * 2f;
                spriteBatch.Draw(
                    smokeTexture,
                    drawPos,
                    frame,
                    new Color(150, 220, 140) * Opacity * glowIntensity * 0.4f,
                    Rotation * 0.8f,
                    origin,
                    Scale * 1.1f,
                    SpriteEffects.None,
                    0f
                );
            }

            return false;
        }
    }
}

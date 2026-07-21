using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    /// <summary>双子火花，EyeMode 0激光眼/1魔焰眼</summary>
    internal class PRT_TwinsSpark : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Extra_193_White";

        public int EyeMode;
        public Color InitialColor;
        public Color GlowColor;
        private float baseScale;
        private float wobble;

        public override bool CanPool => true;
        public void Configure(int lifetime, int eyeMode) {
            Lifetime = lifetime;
            baseScale = Scale;
            EyeMode = eyeMode;
            wobble = Main.rand.NextFloat(MathHelper.TwoPi);
            if (eyeMode == 1) {
                InitialColor = new Color(255, 110, 35);
                GlowColor = new Color(255, 220, 120);
            }
            else {
                InitialColor = new Color(120, 200, 255);
                GlowColor = new Color(180, 130, 255);
            }
            Color = InitialColor;
        }

        public override void Reset() {
            base.Reset();
            EyeMode = 0;
            InitialColor = default;
            GlowColor = default;
            baseScale = 0f;
            wobble = 0f;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
        }

        public override void AI() {
            float t = LifetimeCompletion;

            Velocity *= 0.93f;
            Vector2 perp = new Vector2(-Velocity.Y, Velocity.X).SafeNormalize(Vector2.Zero);
            Velocity += perp * (float)Math.Sin(wobble + t * 8f) * 0.18f;

            Opacity = (float)Math.Sin(t * Math.PI);

            //后段快收
            Scale = baseScale * (1f - t * t * 0.85f);

            Color = Color.Lerp(InitialColor, GlowColor, t * 0.7f);

            Rotation = Velocity.ToRotation();
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = CWRAsset.SoftGlow.Value;
            Vector2 origin = tex.Size() * 0.5f;
            Vector2 drawPos = Position - Main.screenPosition;

            float stretch = MathHelper.Clamp(Velocity.Length() * 0.12f, 1f, 4f);
            Vector2 stretchScale = new Vector2(Scale * 0.18f, Scale * 0.18f * stretch);

            spriteBatch.Draw(tex, drawPos, null, Color * Opacity * 0.6f,
                Rotation + MathHelper.PiOver2, origin, stretchScale * 2.2f, SpriteEffects.None, 0f);

            spriteBatch.Draw(tex, drawPos, null, GlowColor * Opacity * 0.8f,
                Rotation + MathHelper.PiOver2, origin, stretchScale * 1.2f, SpriteEffects.None, 0f);

            spriteBatch.Draw(tex, drawPos, null, Color.White * Opacity,
                Rotation + MathHelper.PiOver2, origin, stretchScale * 0.55f, SpriteEffects.None, 0f);

            return false;
        }
    }
}

using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    /// <summary>引力漩涡螺旋吸入</summary>
    internal class PRT_GravityVortex : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Sparkle";

        private Color initialColor;
        private float initialScale;
        private Vector2 center;
        private float orbitAngle;
        private float orbitRadius;

        public override bool CanPool => true;
        public void Configure(float startAngle, float startRadius, int lifetime) {
            center = Position;
            orbitAngle = startAngle;
            orbitRadius = startRadius;
            initialColor = Color;
            initialScale = Scale;
            Lifetime = lifetime;
            Position = center + orbitAngle.ToRotationVector2() * orbitRadius;
            Velocity = Vector2.Zero;
        }

        public override void Reset() {
            base.Reset();
            initialColor = default;
            initialScale = 0f;
            center = default;
            orbitAngle = 0f;
            orbitRadius = 0f;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
        }

        public override void AI() {
            float life = LifetimeCompletion;

            //开普勒，半径越小越快
            float angularSpeed = 0.08f / Math.Max(orbitRadius * 0.01f, 0.3f);
            orbitAngle += angularSpeed;

            orbitRadius *= 0.97f;
            orbitRadius -= 0.3f;
            if (orbitRadius < 2f) orbitRadius = 2f;

            Position = center + orbitAngle.ToRotationVector2() * orbitRadius;

            float radiusFactor = Math.Min(orbitRadius / 60f, 1f);
            Scale = initialScale * radiusFactor * (1f - life * 0.5f);

            //近心蓝移
            Color blueShift = new Color(180, 200, 255);
            Color = Color.Lerp(initialColor, blueShift, 1f - radiusFactor);

            Opacity = (1f - life) * (0.7f + (1f - radiusFactor) * 0.3f);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D texture = PRTLoader.PRT_IDToTexture[ID];
            Vector2 origin = texture.Size() * 0.5f;
            Vector2 drawPos = Position - Main.screenPosition;

            spriteBatch.Draw(texture, drawPos, null,
                Color * Opacity * 0.4f,
                0f, origin,
                Scale * 1.4f,
                SpriteEffects.None, 0f);

            spriteBatch.Draw(texture, drawPos, null,
                Color * Opacity,
                0f, origin,
                Scale * 0.6f,
                SpriteEffects.None, 0f);

            return false;
        }
    }
}

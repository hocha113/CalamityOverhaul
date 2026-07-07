using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    /// <summary>
    /// 引力漩涡粒子，围绕黑洞中心螺旋吸入
    /// 小巧的光点沿螺旋轨迹向中心坍缩
    /// </summary>
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

            //螺旋向心：角速度随半径减小而加快（开普勒）
            float angularSpeed = 0.08f / Math.Max(orbitRadius * 0.01f, 0.3f);
            orbitAngle += angularSpeed;

            //半径持续收缩（被吸入）
            orbitRadius *= 0.97f;
            orbitRadius -= 0.3f;
            if (orbitRadius < 2f) orbitRadius = 2f;

            //更新位置
            Position = center + orbitAngle.ToRotationVector2() * orbitRadius;

            //缩放随靠近中心而缩小
            float radiusFactor = Math.Min(orbitRadius / 60f, 1f);
            Scale = initialScale * radiusFactor * (1f - life * 0.5f);

            //颜色从暖色变为蓝白（引力蓝移）
            Color blueShift = new Color(180, 200, 255);
            Color = Color.Lerp(initialColor, blueShift, 1f - radiusFactor);

            //接近中心变亮
            Opacity = (1f - life) * (0.7f + (1f - radiusFactor) * 0.3f);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D texture = PRTLoader.PRT_IDToTexture[ID];
            Vector2 origin = texture.Size() * 0.5f;
            Vector2 drawPos = Position - Main.screenPosition;

            //小巧光点
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

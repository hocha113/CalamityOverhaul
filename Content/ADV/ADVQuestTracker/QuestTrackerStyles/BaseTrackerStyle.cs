using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace CalamityOverhaul.Content.ADV.Common.QuestTrackerStyles
{
    /// <summary>
    /// 任务追踪样式基类，提供通用实现
    /// </summary>
    internal abstract class BaseTrackerStyle : IQuestTrackerStyle
    {
        //动画参数
        protected float pulseTimer = 0f;
        protected float animTimer = 0f;

        //粒子列表
        protected readonly List<object> particles = [];

        public virtual void Update(Rectangle panelRect, bool active) {
            pulseTimer += 0.03f;
            animTimer += 0.02f;

            if (pulseTimer > MathHelper.TwoPi) pulseTimer -= MathHelper.TwoPi;
            if (animTimer > MathHelper.TwoPi) animTimer -= MathHelper.TwoPi;
        }

        public abstract void DrawPanel(SpriteBatch spriteBatch, Rectangle panelRect, float alpha);

        public abstract void DrawFrame(SpriteBatch spriteBatch, Rectangle panelRect, float alpha, float borderGlow);

        public abstract void DrawDivider(SpriteBatch spriteBatch, Vector2 start, Vector2 end, float alpha);

        public virtual void DrawProgressBar(SpriteBatch spriteBatch, Rectangle barRect, float progress, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            //背景
            spriteBatch.Draw(pixel, barRect, new Rectangle(0, 0, 1, 1), GetProgressBarBgColor(alpha));

            //进度填充
            float fillWidth = barRect.Width * Math.Min(progress, 1f);

            if (fillWidth > 0) {
                Rectangle barFill = new Rectangle(barRect.X + 1, barRect.Y + 1, (int)fillWidth - 2, barRect.Height - 2);

                Color fillStart = GetProgressBarStartColor(alpha);
                Color fillEnd = GetProgressBarEndColor(alpha);

                //绘制渐变进度条
                int segmentCount = 20;
                for (int i = 0; i < segmentCount; i++) {
                    float t = i / (float)segmentCount;
                    float t2 = (i + 1) / (float)segmentCount;
                    int x1 = (int)(barFill.X + t * barFill.Width);
                    int x2 = (int)(barFill.X + t2 * barFill.Width);

                    Color segColor = Color.Lerp(fillStart, fillEnd, t);
                    float pulse = (float)Math.Sin(pulseTimer * 2f + t * MathHelper.Pi) * 0.3f + 0.7f;

                    spriteBatch.Draw(pixel, new Rectangle(x1, barFill.Y, Math.Max(1, x2 - x1), barFill.Height),
                        segColor * pulse);
                }

                //发光效果
                Color glowColor = GetProgressBarGlowColor(alpha);
                spriteBatch.Draw(pixel, new Rectangle(barFill.X, barFill.Y - 1, barFill.Width, 1), glowColor);
                spriteBatch.Draw(pixel, new Rectangle(barFill.X, barFill.Bottom, barFill.Width, 1), glowColor);
            }

            //边框
            Color borderColor = GetProgressBarBorderColor(alpha);
            spriteBatch.Draw(pixel, new Rectangle(barRect.X, barRect.Y, barRect.Width, 1), borderColor);
            spriteBatch.Draw(pixel, new Rectangle(barRect.X, barRect.Bottom - 1, barRect.Width, 1), borderColor);
            spriteBatch.Draw(pixel, new Rectangle(barRect.X, barRect.Y, 1, barRect.Height), borderColor);
            spriteBatch.Draw(pixel, new Rectangle(barRect.Right - 1, barRect.Y, 1, barRect.Height), borderColor);
        }

        public abstract Color GetTitleColor(float alpha);
        public abstract Color GetTextColor(float alpha);
        public abstract Color GetNumberColor(float progress, float targetProgress, float alpha);

        protected abstract Color GetProgressBarBgColor(float alpha);
        protected abstract Color GetProgressBarStartColor(float alpha);
        protected abstract Color GetProgressBarEndColor(float alpha);
        protected abstract Color GetProgressBarGlowColor(float alpha);
        protected abstract Color GetProgressBarBorderColor(float alpha);

        public virtual void Reset() {
            pulseTimer = 0f;
            animTimer = 0f;
            particles.Clear();
        }

        public virtual void GetParticles(out List<object> particles) {
            particles = this.particles;
        }

        public virtual void UpdateParticles(Vector2 basePos, float panelFade) { }

        /// <summary>
        /// 绘制渐变线
        /// </summary>
        protected static void DrawGradientLine(SpriteBatch spriteBatch, Vector2 start, Vector2 end, Color startColor, Color endColor, float thickness) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Vector2 edge = end - start;
            float length = edge.Length();
            if (length < 1f) return;

            edge.Normalize();
            float rotation = (float)Math.Atan2(edge.Y, edge.X);
            int segments = Math.Max(1, (int)(length / 10f));

            for (int i = 0; i < segments; i++) {
                float t = i / (float)segments;
                Vector2 segPos = start + edge * (length * t);
                float segLength = length / segments;
                Color color = Color.Lerp(startColor, endColor, t);
                spriteBatch.Draw(pixel, segPos, new Rectangle(0, 0, 1, 1), color, rotation, new Vector2(0, 0.5f), new Vector2(segLength, thickness), SpriteEffects.None, 0);
            }
        }
    }
}

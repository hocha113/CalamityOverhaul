using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.ADV.ADVQuestTracker.QuestTrackerStyles
{
    /// <summary>
    /// 硫磺海风格
    /// </summary>
    internal class SulfseaTrackerStyle : BaseTrackerStyle
    {
        //硫磺海风格动画参数
        private float toxicWavePhase;
        private float sulfurPulse;
        private float miasmaTimer;
        private float bubbleTimer;

        public override void Update(Rectangle panelRect, bool active) {
            base.Update(panelRect, active);

            //更新硫磺海风格动画
            toxicWavePhase += 0.022f;
            sulfurPulse += 0.015f;
            miasmaTimer += 0.032f;
            bubbleTimer += 0.025f;

            if (toxicWavePhase > MathHelper.TwoPi) toxicWavePhase -= MathHelper.TwoPi;
            if (sulfurPulse > MathHelper.TwoPi) sulfurPulse -= MathHelper.TwoPi;
            if (miasmaTimer > MathHelper.TwoPi) miasmaTimer -= MathHelper.TwoPi;
            if (bubbleTimer > MathHelper.TwoPi) bubbleTimer -= MathHelper.TwoPi;
        }

        public override void DrawPanel(SpriteBatch spriteBatch, Rectangle panelRect, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            //绘制阴影
            Rectangle shadowRect = panelRect;
            shadowRect.Offset(6, 8);
            spriteBatch.Draw(pixel, shadowRect, new Rectangle(0, 0, 1, 1), Color.Black * (alpha * 0.6f));

            //绘制硫磺海渐变背景
            DrawSulfurBackground(spriteBatch, panelRect, alpha);

            //绘制瘴气覆盖层
            float miasmaEffect = (float)Math.Sin(miasmaTimer * 1.1f) * 0.5f + 0.5f;
            Color miasmaTint = new Color(45, 55, 20) * (alpha * 0.4f * miasmaEffect);
            spriteBatch.Draw(pixel, panelRect, new Rectangle(0, 0, 1, 1), miasmaTint);

            //绘制毒性波浪覆盖
            DrawToxicWaveOverlay(spriteBatch, panelRect, alpha * 0.85f);

            //绘制气泡装饰
            DrawBubbleDecoration(spriteBatch, panelRect, alpha);
        }

        private void DrawSulfurBackground(SpriteBatch spriteBatch, Rectangle panelRect, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            int segs = 30;

            for (int i = 0; i < segs; i++) {
                float t = i / (float)segs;
                float t2 = (i + 1) / (float)segs;
                int y1 = (int)(panelRect.Y + t * panelRect.Height);
                int y2 = (int)(panelRect.Y + t2 * panelRect.Height);
                Rectangle r = new Rectangle(panelRect.X, y1, panelRect.Width, Math.Max(1, y2 - y1));

                //硫磺海配色
                Color sulfurDeep = new Color(12, 18, 8);
                Color toxicMid = new Color(28, 38, 15);
                Color acidEdge = new Color(65, 85, 30);

                float breathing = (float)Math.Sin(sulfurPulse) * 0.5f + 0.5f;
                Color blendBase = Color.Lerp(sulfurDeep, toxicMid, (float)Math.Sin(pulseTimer * 0.5f + t * 1.4f) * 0.5f + 0.5f);
                Color c = Color.Lerp(blendBase, acidEdge, t * 0.7f * (0.3f + breathing * 0.7f));
                c *= alpha * 0.92f;

                spriteBatch.Draw(pixel, r, new Rectangle(0, 0, 1, 1), c);
            }
        }

        private void DrawToxicWaveOverlay(SpriteBatch sb, Rectangle rect, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            int bands = 5;

            for (int i = 0; i < bands; i++) {
                float t = i / (float)bands;
                float y = rect.Y + 15 + t * (rect.Height - 30);
                float amp = 5f + (float)Math.Sin((toxicWavePhase + t) * 2.2f) * 3.5f;
                float thickness = 1.8f;
                int segments = 35;
                Vector2 prev = Vector2.Zero;

                for (int s = 0; s <= segments; s++) {
                    float p = s / (float)segments;
                    float localY = y + (float)Math.Sin(toxicWavePhase * 2.2f + p * MathHelper.TwoPi * 1.3f + t) * amp;
                    Vector2 point = new(rect.X + 8 + p * (rect.Width - 16), localY);

                    if (s > 0) {
                        Vector2 diff = point - prev;
                        float len = diff.Length();
                        if (len > 0.01f) {
                            float rot = diff.ToRotation();
                            Color c = new Color(60, 90, 30) * (alpha * 0.08f);
                            sb.Draw(pixel, prev, new Rectangle(0, 0, 1, 1), c, rot, Vector2.Zero, new Vector2(len, thickness), SpriteEffects.None, 0f);
                        }
                    }
                    prev = point;
                }
            }
        }

        private void DrawBubbleDecoration(SpriteBatch spriteBatch, Rectangle rect, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            //绘制几个漂浮的硫磺气泡
            for (int i = 0; i < 4; i++) {
                float offset = (bubbleTimer + i * MathHelper.PiOver2) % MathHelper.TwoPi;
                float yPos = rect.Y + 20 + (float)Math.Sin(offset) * 15f + i * 30f;
                float xPos = rect.X + 15 + i * 60f;
                xPos *= rect.Width / 220f;

                if (yPos > rect.Y + 10 && yPos < rect.Bottom - 10) {
                    float bubbleSize = 3f + (float)Math.Sin(offset * 2f) * 1.5f;
                    Color bubbleColor = new Color(140, 180, 70) * (alpha * 0.35f);

                    spriteBatch.Draw(pixel, new Vector2(xPos, yPos), new Rectangle(0, 0, 1, 1), bubbleColor, 0f,
                        new Vector2(0.5f), new Vector2(bubbleSize), SpriteEffects.None, 0f);
                }
            }
        }

        public override void DrawFrame(SpriteBatch spriteBatch, Rectangle panelRect, float alpha, float borderGlow) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Color edge = Color.Lerp(new Color(70, 100, 35), new Color(130, 160, 65), borderGlow) * (alpha * 0.85f);

            //外边框
            spriteBatch.Draw(pixel, new Rectangle(panelRect.X, panelRect.Y, panelRect.Width, 2), new Rectangle(0, 0, 1, 1), edge);
            spriteBatch.Draw(pixel, new Rectangle(panelRect.X, panelRect.Bottom - 2, panelRect.Width, 2), new Rectangle(0, 0, 1, 1), edge * 0.75f);
            spriteBatch.Draw(pixel, new Rectangle(panelRect.X, panelRect.Y, 2, panelRect.Height), new Rectangle(0, 0, 1, 1), edge * 0.88f);
            spriteBatch.Draw(pixel, new Rectangle(panelRect.Right - 2, panelRect.Y, 2, panelRect.Height), new Rectangle(0, 0, 1, 1), edge * 0.88f);

            //内边框
            Rectangle inner = panelRect;
            inner.Inflate(-5, -5);
            Color innerC = new Color(140, 170, 70) * (alpha * 0.22f * borderGlow);
            spriteBatch.Draw(pixel, new Rectangle(inner.X, inner.Y, inner.Width, 1), new Rectangle(0, 0, 1, 1), innerC);
            spriteBatch.Draw(pixel, new Rectangle(inner.X, inner.Bottom - 1, inner.Width, 1), new Rectangle(0, 0, 1, 1), innerC * 0.7f);
            spriteBatch.Draw(pixel, new Rectangle(inner.X, inner.Y, 1, inner.Height), new Rectangle(0, 0, 1, 1), innerC * 0.88f);
            spriteBatch.Draw(pixel, new Rectangle(inner.Right - 1, inner.Y, 1, inner.Height), new Rectangle(0, 0, 1, 1), innerC * 0.88f);

            //角落装饰
            DrawCornerStar(spriteBatch, new Vector2(panelRect.X + 10, panelRect.Y + 10), alpha * 0.9f, borderGlow);
            DrawCornerStar(spriteBatch, new Vector2(panelRect.Right - 10, panelRect.Y + 10), alpha * 0.9f, borderGlow);
            DrawCornerStar(spriteBatch, new Vector2(panelRect.X + 10, panelRect.Bottom - 10), alpha * 0.65f, borderGlow);
            DrawCornerStar(spriteBatch, new Vector2(panelRect.Right - 10, panelRect.Bottom - 10), alpha * 0.65f, borderGlow);
        }

        private static void DrawCornerStar(SpriteBatch sb, Vector2 pos, float a, float pulse) {
            Texture2D px = VaultAsset.placeholder2.Value;
            float size = 5f + (float)Math.Sin(pulse * MathHelper.TwoPi) * 1f;
            Color c = new Color(160, 190, 80) * (a * (0.8f + pulse * 0.2f));

            sb.Draw(px, pos, new Rectangle(0, 0, 1, 1), c, 0f, new Vector2(0.5f, 0.5f), new Vector2(size, size * 0.26f), SpriteEffects.None, 0f);
            sb.Draw(px, pos, new Rectangle(0, 0, 1, 1), c * 0.8f, MathHelper.PiOver2, new Vector2(0.5f, 0.5f), new Vector2(size, size * 0.26f), SpriteEffects.None, 0f);
        }

        public override void DrawDivider(SpriteBatch spriteBatch, Vector2 start, Vector2 end, float alpha) {
            DrawGradientLine(spriteBatch, start, end,
                new Color(100, 140, 50) * (alpha * 0.9f), new Color(100, 140, 50) * (alpha * 0.08f), 1.3f);
        }

        public override Color GetTitleColor(float alpha) {
            return new Color(160, 190, 80) * alpha;
        }

        public override Color GetTextColor(float alpha) {
            return new Color(200, 220, 150) * alpha;
        }

        public override Color GetNumberColor(float progress, float targetProgress, float alpha) {
            if (progress >= targetProgress) {
                float pulse = (float)Math.Sin(pulseTimer * 3f) * 0.3f + 0.7f;
                return Color.LimeGreen * (alpha * pulse);
            }
            return new Color(180, 200, 130) * alpha;
        }

        protected override Color GetProgressBarBgColor(float alpha) {
            return new Color(10, 15, 8) * (alpha * 0.8f);
        }

        protected override Color GetProgressBarStartColor(float alpha) {
            return new Color(100, 140, 50) * alpha;
        }

        protected override Color GetProgressBarEndColor(float alpha) {
            return new Color(160, 190, 80) * alpha;
        }

        protected override Color GetProgressBarGlowColor(float alpha) {
            return new Color(160, 190, 80) * (alpha * 0.4f);
        }

        protected override Color GetProgressBarBorderColor(float alpha) {
            return new Color(100, 140, 50) * (alpha * 0.8f);
        }
    }
}

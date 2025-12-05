using Microsoft.Xna.Framework.Graphics;
using System;

namespace CalamityOverhaul.Content.ADV.Common.QuestTrackerStyles
{
    /// <summary>
    /// 硫磺火风格（默认BaseQuestTrackerUI风格）
    /// </summary>
    internal class BrimstoneTrackerStyle : BaseTrackerStyle
    {
        public override void DrawPanel(SpriteBatch spriteBatch, Rectangle panelRect, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            //阴影
            Rectangle shadowRect = panelRect;
            shadowRect.Offset(3, 3);
            spriteBatch.Draw(pixel, shadowRect, new Rectangle(0, 0, 1, 1), Color.Black * (alpha * 0.5f));

            //背景渐变 (硫磺火风格)
            int segments = 15;
            for (int i = 0; i < segments; i++) {
                float t = i / (float)segments;
                float t2 = (i + 1) / (float)segments;
                int y1 = (int)(panelRect.Y + t * panelRect.Height);
                int y2 = (int)(panelRect.Y + t2 * panelRect.Height);
                Rectangle r = new Rectangle(panelRect.X, y1, panelRect.Width, Math.Max(1, y2 - y1));

                Color deep = new Color(30, 15, 15);
                Color mid = new Color(70, 30, 25);
                Color hot = new Color(120, 50, 35);

                float wave = (float)Math.Sin(pulseTimer * 1.2f + t * 2f) * 0.5f + 0.5f;
                Color c = Color.Lerp(Color.Lerp(deep, mid, wave), hot, t * 0.5f);
                c *= alpha;

                spriteBatch.Draw(pixel, r, new Rectangle(0, 0, 1, 1), c);
            }

            //火焰脉冲效果
            float pulse = (float)Math.Sin(pulseTimer * 2f) * 0.5f + 0.5f;
            Color pulseColor = new Color(140, 40, 25) * (alpha * 0.15f * pulse);
            spriteBatch.Draw(pixel, panelRect, new Rectangle(0, 0, 1, 1), pulseColor);
        }

        public override void DrawFrame(SpriteBatch spriteBatch, Rectangle panelRect, float alpha, float borderGlow) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            //外框
            Color outerEdge = Color.Lerp(new Color(180, 60, 30), new Color(255, 140, 70), borderGlow) * (alpha * 0.85f);
            spriteBatch.Draw(pixel, new Rectangle(panelRect.X, panelRect.Y, panelRect.Width, 2), new Rectangle(0, 0, 1, 1), outerEdge);
            spriteBatch.Draw(pixel, new Rectangle(panelRect.X, panelRect.Bottom - 2, panelRect.Width, 2), new Rectangle(0, 0, 1, 1), outerEdge * 0.75f);
            spriteBatch.Draw(pixel, new Rectangle(panelRect.X, panelRect.Y, 2, panelRect.Height), new Rectangle(0, 0, 1, 1), outerEdge * 0.9f);
            spriteBatch.Draw(pixel, new Rectangle(panelRect.Right - 2, panelRect.Y, 2, panelRect.Height), new Rectangle(0, 0, 1, 1), outerEdge * 0.9f);

            //内框发光
            Rectangle inner = panelRect;
            inner.Inflate(-5, -5);
            Color innerGlow = new Color(220, 100, 50) * (alpha * 0.22f * borderGlow);
            spriteBatch.Draw(pixel, new Rectangle(inner.X, inner.Y, inner.Width, 1), new Rectangle(0, 0, 1, 1), innerGlow);
            spriteBatch.Draw(pixel, new Rectangle(inner.X, inner.Bottom - 1, inner.Width, 1), new Rectangle(0, 0, 1, 1), innerGlow * 0.7f);
            spriteBatch.Draw(pixel, new Rectangle(inner.X, inner.Y, 1, inner.Height), new Rectangle(0, 0, 1, 1), innerGlow * 0.85f);
            spriteBatch.Draw(pixel, new Rectangle(inner.Right - 1, inner.Y, 1, inner.Height), new Rectangle(0, 0, 1, 1), innerGlow * 0.85f);
        }

        public override void DrawDivider(SpriteBatch spriteBatch, Vector2 start, Vector2 end, float alpha) {
            DrawGradientLine(spriteBatch, start, end,
                Color.OrangeRed * alpha * 0.8f, Color.OrangeRed * alpha * 0.1f, 1.2f);
        }

        public override Color GetTitleColor(float alpha) {
            return new Color(255, 220, 180) * alpha;
        }

        public override Color GetTextColor(float alpha) {
            return Color.White * alpha;
        }

        public override Color GetNumberColor(float progress, float targetProgress, float alpha) {
            if (progress >= targetProgress) {
                return Color.LimeGreen * alpha;
            }
            return Color.Lerp(new Color(100, 200, 255), Color.Cyan, progress / targetProgress) * alpha;
        }

        protected override Color GetProgressBarBgColor(float alpha) {
            return Color.Black * (alpha * 0.6f);
        }

        protected override Color GetProgressBarStartColor(float alpha) {
            return new Color(180, 50, 50) * alpha;
        }

        protected override Color GetProgressBarEndColor(float alpha) {
            return new Color(255, 140, 60) * alpha;
        }

        protected override Color GetProgressBarGlowColor(float alpha) {
            return Color.OrangeRed * (alpha * 0.6f);
        }

        protected override Color GetProgressBarBorderColor(float alpha) {
            return Color.OrangeRed * (alpha * 0.6f);
        }
    }
}

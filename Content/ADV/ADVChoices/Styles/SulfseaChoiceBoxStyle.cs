using CalamityOverhaul.Content.ADV.UIEffect;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.ADV.ADVChoices.Styles
{
    /// <summary>
    /// 硫磺海风格选项框样式
    /// </summary>
    internal class SulfseaChoiceBoxStyle : IChoiceBoxStyle
    {
        //动画计时器
        private float toxicWavePhase = 0f;
        private float sulfurPulse = 0f;
        private float miasmaTimer = 0f;

        //粒子系统
        private readonly List<BubblePRT> bubbles = [];
        private int bubbleSpawnTimer = 0;
        private readonly List<AshPRT> ashParticles = [];
        private int ashSpawnTimer = 0;

        private const float BubbleSideMargin = 20f;

        public void Update(Rectangle panelRect, bool active, bool closing) {
            //更新动画计时器
            toxicWavePhase += 0.022f;
            sulfurPulse += 0.015f;
            miasmaTimer += 0.032f;

            if (toxicWavePhase > MathHelper.TwoPi) toxicWavePhase -= MathHelper.TwoPi;
            if (sulfurPulse > MathHelper.TwoPi) sulfurPulse -= MathHelper.TwoPi;
            if (miasmaTimer > MathHelper.TwoPi) miasmaTimer -= MathHelper.TwoPi;

            //气泡粒子更新
            float scaleW = Main.UIScale;
            bubbleSpawnTimer++;
            if (active && !closing && bubbleSpawnTimer >= 12 && bubbles.Count < 15) {
                bubbleSpawnTimer = 0;
                float left = panelRect.X + BubbleSideMargin * scaleW;
                float right = panelRect.Right - BubbleSideMargin * scaleW;
                Vector2 start = new(Main.rand.NextFloat(left, right), panelRect.Bottom - 8f);
                var bb = new BubblePRT(start);
                bb.CoreColor = Color.LightYellow;
                bb.RimColor = Color.LimeGreen;
                bubbles.Add(bb);
            }

            for (int i = bubbles.Count - 1; i >= 0; i--) {
                Vector2 panelPos = new(panelRect.X, panelRect.Y);
                Vector2 panelSize = new(panelRect.Width, panelRect.Height);
                if (bubbles[i].Update(panelPos, panelSize, BubbleSideMargin)) {
                    bubbles.RemoveAt(i);
                }
            }

            //灰烬粒子更新
            ashSpawnTimer++;
            if (active && !closing && ashSpawnTimer >= 18 && ashParticles.Count < 10) {
                ashSpawnTimer = 0;
                float left = panelRect.X + BubbleSideMargin * scaleW;
                float right = panelRect.Right - BubbleSideMargin * scaleW;
                Vector2 start = new(Main.rand.NextFloat(left, right), panelRect.Bottom - 8f);
                ashParticles.Add(new AshPRT(start));
            }

            for (int i = ashParticles.Count - 1; i >= 0; i--) {
                Vector2 panelPos = new(panelRect.X, panelRect.Y);
                Vector2 panelSize = new(panelRect.Width, panelRect.Height);
                if (ashParticles[i].Update(panelPos, panelSize)) {
                    ashParticles.RemoveAt(i);
                }
            }
        }

        public void Draw(SpriteBatch spriteBatch, Rectangle panelRect, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            //绘制阴影
            Rectangle shadowRect = panelRect;
            shadowRect.Offset(6, 8);
            spriteBatch.Draw(pixel, shadowRect, new Rectangle(0, 0, 1, 1), Color.Black * (alpha * 0.60f));

            //绘制渐变背景 - 硫磺海配色
            int segments = 30;
            for (int i = 0; i < segments; i++) {
                float t = i / (float)segments;
                float t2 = (i + 1) / (float)segments;
                int y1 = panelRect.Y + (int)(t * panelRect.Height);
                int y2 = panelRect.Y + (int)(t2 * panelRect.Height);
                Rectangle r = new(panelRect.X, y1, panelRect.Width, Math.Max(1, y2 - y1));

                //硫磺海配色:深绿、黄绿、带毒的黄色
                Color sulfurDeep = new Color(12, 18, 8);
                Color toxicMid = new Color(28, 38, 15);
                Color acidEdge = new Color(65, 85, 30);

                float breathing = (float)Math.Sin(sulfurPulse) * 0.5f + 0.5f;
                Color blendBase = Color.Lerp(sulfurDeep, toxicMid, (float)Math.Sin(sulfurPulse * 0.5f + t * 1.4f) * 0.5f + 0.5f);
                Color c = Color.Lerp(blendBase, acidEdge, t * 0.7f * (0.3f + breathing * 0.7f));
                c *= alpha * 0.92f;

                spriteBatch.Draw(pixel, r, new Rectangle(0, 0, 1, 1), c);
            }

            //瘴气覆盖层
            float miasmaEffect = (float)Math.Sin(miasmaTimer * 1.1f) * 0.5f + 0.5f;
            Color miasmaTint = new Color(45, 55, 20) * (alpha * 0.4f * miasmaEffect);
            spriteBatch.Draw(pixel, panelRect, new Rectangle(0, 0, 1, 1), miasmaTint);

            //绘制毒波纹理效果
            DrawToxicWaveOverlay(spriteBatch, panelRect, alpha * 0.85f);

            //绘制内发光
            float pulse = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 2.2f) * 0.5f + 0.5f;
            Rectangle inner = panelRect;
            inner.Inflate(-6, -6);
            spriteBatch.Draw(pixel, inner, new Rectangle(0, 0, 1, 1), new Color(80, 100, 35) * (alpha * 0.09f * (0.5f + pulse * 0.5f)));

            //绘制边框
            DrawFrameSulfur(spriteBatch, panelRect, alpha, pulse);

            //绘制粒子效果
            foreach (var ash in ashParticles) {
                ash.Draw(spriteBatch, alpha * 0.75f);
            }
            foreach (var b in bubbles) {
                b.DrawEnhanced(spriteBatch, alpha * 0.9f);
            }
        }

        public void DrawChoiceBackground(SpriteBatch spriteBatch, Rectangle choiceRect, bool enabled, float hoverProgress, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            //选项背景
            Color choiceBg = enabled
                ? Color.Lerp(new Color(20, 30, 10) * 0.3f, new Color(50, 70, 25) * 0.5f, hoverProgress)
                : new Color(15, 20, 10) * 0.15f;

            spriteBatch.Draw(pixel, choiceRect, new Rectangle(0, 0, 1, 1), choiceBg * alpha);

            //选项边框
            Color edgeColor = GetEdgeColor(alpha);
            if (enabled && hoverProgress > 0.01f) {
                DrawChoiceBorder(spriteBatch, choiceRect, edgeColor * (hoverProgress * 0.6f));

                //悬停时的毒液效果
                float toxicGlow = (float)Math.Sin(toxicWavePhase * 2f + hoverProgress * 3f) * 0.5f + 0.5f;
                Color toxicColor = new Color(100, 140, 50) * (alpha * 0.15f * hoverProgress * toxicGlow);
                spriteBatch.Draw(pixel, choiceRect, new Rectangle(0, 0, 1, 1), toxicColor);
            }
            else if (!enabled) {
                DrawChoiceBorder(spriteBatch, choiceRect, new Color(60, 80, 35) * (alpha * 0.25f));
            }
        }

        public Color GetEdgeColor(float alpha) {
            float pulse = (float)Math.Sin(sulfurPulse) * 0.5f + 0.5f;
            return Color.Lerp(new Color(70, 100, 35), new Color(130, 160, 65), pulse) * (alpha * 0.85f);
        }

        public Color GetTextGlowColor(float alpha, float hoverProgress) {
            float toxicGlow = (float)Math.Sin(toxicWavePhase * 2f) * 0.5f + 0.5f;
            return new Color(160, 190, 80) * (alpha * (0.5f + toxicGlow * 0.5f));
        }

        public void DrawTitleDecoration(SpriteBatch spriteBatch, Vector2 titlePos, string title, float alpha) {
            Color glowColor = new Color(160, 190, 80) * (alpha * 0.75f);
            for (int i = 0; i < 4; i++) {
                float ang = MathHelper.TwoPi * i / 4f;
                Vector2 offset = ang.ToRotationVector2() * 1.8f;
                Utils.DrawBorderString(spriteBatch, title, titlePos + offset, glowColor * 0.6f, 0.9f);
            }
        }

        public void DrawDivider(SpriteBatch spriteBatch, Vector2 start, Vector2 end, float alpha) {
            Color edgeColor = GetEdgeColor(alpha);
            DrawGradientLine(spriteBatch, start, end, edgeColor * 0.9f, edgeColor * 0.08f, 1.3f);
        }

        public void Reset() {
            toxicWavePhase = 0f;
            sulfurPulse = 0f;
            miasmaTimer = 0f;
            bubbles.Clear();
            ashParticles.Clear();
            bubbleSpawnTimer = 0;
            ashSpawnTimer = 0;
        }

        #region 辅助方法
        private void DrawToxicWaveOverlay(SpriteBatch sb, Rectangle rect, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            int bands = 6;
            for (int i = 0; i < bands; i++) {
                float t = i / (float)bands;
                float y = rect.Y + 18 + t * (rect.Height - 36);
                float amp = 7f + (float)Math.Sin((toxicWavePhase + t) * 2.2f) * 4.5f;
                float thickness = 2.2f;
                int segments = 42;
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

        private static void DrawFrameSulfur(SpriteBatch sb, Rectangle rect, float alpha, float pulse) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Color edge = Color.Lerp(new Color(70, 100, 35), new Color(130, 160, 65), pulse) * (alpha * 0.85f);

            //绘制主边框
            sb.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, 2), new Rectangle(0, 0, 1, 1), edge);
            sb.Draw(pixel, new Rectangle(rect.X, rect.Bottom - 2, rect.Width, 2), new Rectangle(0, 0, 1, 1), edge * 0.75f);
            sb.Draw(pixel, new Rectangle(rect.X, rect.Y, 2, rect.Height), new Rectangle(0, 0, 1, 1), edge * 0.88f);
            sb.Draw(pixel, new Rectangle(rect.Right - 2, rect.Y, 2, rect.Height), new Rectangle(0, 0, 1, 1), edge * 0.88f);

            //绘制内边框
            Rectangle inner = rect;
            inner.Inflate(-5, -5);
            Color innerC = new Color(140, 170, 70) * (alpha * 0.22f * pulse);
            sb.Draw(pixel, new Rectangle(inner.X, inner.Y, inner.Width, 1), new Rectangle(0, 0, 1, 1), innerC);
            sb.Draw(pixel, new Rectangle(inner.X, inner.Bottom - 1, inner.Width, 1), new Rectangle(0, 0, 1, 1), innerC * 0.7f);
            sb.Draw(pixel, new Rectangle(inner.X, inner.Y, 1, inner.Height), new Rectangle(0, 0, 1, 1), innerC * 0.88f);
            sb.Draw(pixel, new Rectangle(inner.Right - 1, inner.Y, 1, inner.Height), new Rectangle(0, 0, 1, 1), innerC * 0.88f);

            //绘制角星装饰
            DrawCornerStar(sb, new Vector2(rect.X + 10, rect.Y + 10), alpha * 0.9f);
            DrawCornerStar(sb, new Vector2(rect.Right - 10, rect.Y + 10), alpha * 0.9f);
            DrawCornerStar(sb, new Vector2(rect.X + 10, rect.Bottom - 10), alpha * 0.65f);
            DrawCornerStar(sb, new Vector2(rect.Right - 10, rect.Bottom - 10), alpha * 0.65f);
        }

        private static void DrawCornerStar(SpriteBatch sb, Vector2 pos, float a) {
            Texture2D px = VaultAsset.placeholder2.Value;
            float size = 5f;
            Color c = new Color(160, 190, 80) * a;
            sb.Draw(px, pos, new Rectangle(0, 0, 1, 1), c, 0f, new Vector2(0.5f, 0.5f), new Vector2(size, size * 0.26f), SpriteEffects.None, 0f);
            sb.Draw(px, pos, new Rectangle(0, 0, 1, 1), c * 0.8f, MathHelper.PiOver2, new Vector2(0.5f, 0.5f), new Vector2(size, size * 0.26f), SpriteEffects.None, 0f);
        }

        private static void DrawChoiceBorder(SpriteBatch spriteBatch, Rectangle rect, Color color) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, 1),
                new Rectangle(0, 0, 1, 1), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1),
                new Rectangle(0, 0, 1, 1), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, 1, rect.Height),
                new Rectangle(0, 0, 1, 1), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height),
                new Rectangle(0, 0, 1, 1), color);
        }

        private static void DrawGradientLine(SpriteBatch spriteBatch, Vector2 start, Vector2 end,
            Color startColor, Color endColor, float thickness) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Vector2 edge = end - start;
            float length = edge.Length();
            if (length < 1f) return;

            edge.Normalize();
            float rotation = (float)Math.Atan2(edge.Y, edge.X);
            int segments = Math.Max(1, (int)(length / 11f));

            for (int i = 0; i < segments; i++) {
                float t = i / (float)segments;
                Vector2 segPos = start + edge * (length * t);
                float segLength = length / segments;
                Color color = Color.Lerp(startColor, endColor, t);
                spriteBatch.Draw(pixel, segPos, new Rectangle(0, 0, 1, 1), color, rotation,
                    new Vector2(0, 0.5f), new Vector2(segLength, thickness), SpriteEffects.None, 0);
            }
        }
        #endregion
    }
}

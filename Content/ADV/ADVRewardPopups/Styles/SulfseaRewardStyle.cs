using CalamityOverhaul.Content.ADV.UIEffect;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.ADV.ADVRewardPopups.Styles
{
    /// <summary>
    /// 硫磺海奖励风格
    /// </summary>
    internal class SulfseaRewardStyle : IRewardPopupStyle
    {
        private float toxicWavePhase = 0f;
        private float sulfurPulse = 0f;
        private float miasmaTimer = 0f;
        private readonly List<BubblePRT> bubbles = [];
        private readonly List<AshPRT> ashParticles = [];
        private int bubbleTimer = 0;
        private int ashTimer = 0;

        public void Update(Rectangle panelRect, bool active, bool closing) {
            toxicWavePhase += 0.022f;
            sulfurPulse += 0.015f;
            miasmaTimer += 0.032f;

            if (toxicWavePhase > MathHelper.TwoPi) toxicWavePhase -= MathHelper.TwoPi;
            if (sulfurPulse > MathHelper.TwoPi) sulfurPulse -= MathHelper.TwoPi;
            if (miasmaTimer > MathHelper.TwoPi) miasmaTimer -= MathHelper.TwoPi;
        }

        public void DrawPanel(SpriteBatch spriteBatch, Rectangle rect, float alpha, float hoverGlow) {
            Texture2D px = VaultAsset.placeholder2.Value;

            //绘制渐变背景 - 硫磺海配色
            int segments = 26;
            for (int i = 0; i < segments; i++) {
                float t = i / (float)segments;
                float t2 = (i + 1) / (float)segments;
                int y1 = rect.Y + (int)(t * rect.Height);
                int y2 = rect.Y + (int)(t2 * rect.Height);
                Rectangle r = new(rect.X, y1, rect.Width, Math.Max(1, y2 - y1));

                //硫磺海配色:深绿、黄绿、带毒的黄色
                Color sulfurDeep = new Color(12, 18, 8);
                Color toxicMid = new Color(28, 38, 15);
                Color acidEdge = new Color(65, 85, 30);

                float breathing = (float)Math.Sin(sulfurPulse) * 0.5f + 0.5f;
                Color blendBase = Color.Lerp(sulfurDeep, toxicMid, (float)Math.Sin(sulfurPulse * 0.5f + t * 1.4f) * 0.5f + 0.5f);
                Color c = Color.Lerp(blendBase, acidEdge, t * 0.7f * (0.3f + breathing * 0.7f));
                c *= alpha * (0.92f + hoverGlow * 0.15f);

                spriteBatch.Draw(px, r, new Rectangle(0, 0, 1, 1), c);
            }

            //瘴气覆盖层
            float miasmaEffect = (float)Math.Sin(miasmaTimer * 1.1f) * 0.5f + 0.5f;
            Color miasmaTint = new Color(45, 55, 20) * (alpha * (0.4f + hoverGlow * 0.2f) * miasmaEffect);
            spriteBatch.Draw(px, rect, new Rectangle(0, 0, 1, 1), miasmaTint);

            //绘制毒波纹理效果
            DrawToxicWaveOverlay(spriteBatch, rect, alpha * (0.85f + hoverGlow * 0.15f));

            //内边框微光
            Rectangle inner = rect;
            inner.Inflate(-6, -6);
            float pulse = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 2.2f) * 0.5f + 0.5f;
            spriteBatch.Draw(px, inner, new Rectangle(0, 0, 1, 1), new Color(80, 100, 35) * (alpha * (0.09f + hoverGlow * 0.4f) * (0.5f + pulse * 0.5f)));
        }

        public void DrawFrame(SpriteBatch spriteBatch, Rectangle rect, float alpha, float hoverGlow) {
            Texture2D px = VaultAsset.placeholder2.Value;
            float pulse = (float)Math.Sin(sulfurPulse) * 0.5f + 0.5f;
            Color edge = Color.Lerp(new Color(70, 100, 35), new Color(130, 160, 65), pulse) * (alpha * (0.85f + hoverGlow * 0.3f));

            //绘制主边框
            spriteBatch.Draw(px, new Rectangle(rect.X, rect.Y, rect.Width, 2), new Rectangle(0, 0, 1, 1), edge);
            spriteBatch.Draw(px, new Rectangle(rect.X, rect.Bottom - 2, rect.Width, 2), new Rectangle(0, 0, 1, 1), edge * 0.75f);
            spriteBatch.Draw(px, new Rectangle(rect.X, rect.Y, 2, rect.Height), new Rectangle(0, 0, 1, 1), edge * 0.88f);
            spriteBatch.Draw(px, new Rectangle(rect.Right - 2, rect.Y, 2, rect.Height), new Rectangle(0, 0, 1, 1), edge * 0.88f);

            //绘制内边框
            Rectangle inner = rect;
            inner.Inflate(-5, -5);
            Color innerC = new Color(140, 170, 70) * (alpha * 0.22f * pulse * (1f + hoverGlow * 0.5f));
            spriteBatch.Draw(px, new Rectangle(inner.X, inner.Y, inner.Width, 1), new Rectangle(0, 0, 1, 1), innerC);
            spriteBatch.Draw(px, new Rectangle(inner.X, inner.Bottom - 1, inner.Width, 1), new Rectangle(0, 0, 1, 1), innerC * 0.7f);
            spriteBatch.Draw(px, new Rectangle(inner.X, inner.Y, 1, inner.Height), new Rectangle(0, 0, 1, 1), innerC * 0.88f);
            spriteBatch.Draw(px, new Rectangle(inner.Right - 1, inner.Y, 1, inner.Height), new Rectangle(0, 0, 1, 1), innerC * 0.88f);

            //绘制角星装饰
            DrawCornerStar(spriteBatch, new Vector2(rect.X + 10, rect.Y + 10), alpha * (0.9f + hoverGlow * 0.4f));
            DrawCornerStar(spriteBatch, new Vector2(rect.Right - 10, rect.Y + 10), alpha * (0.9f + hoverGlow * 0.4f));
            DrawCornerStar(spriteBatch, new Vector2(rect.X + 10, rect.Bottom - 10), alpha * (0.65f + hoverGlow * 0.3f));
            DrawCornerStar(spriteBatch, new Vector2(rect.Right - 10, rect.Bottom - 10), alpha * (0.65f + hoverGlow * 0.3f));
        }

        public Color GetNameGlowColor(float alpha) {
            return new Color(160, 190, 80) * (alpha * 0.75f);
        }

        public Color GetNameColor(float alpha) {
            return Color.White * alpha;
        }

        public Color GetHintColor(float alpha, float blink) {
            float toxicGlow = (float)Math.Sin(toxicWavePhase * 2f) * 0.5f + 0.5f;
            return new Color(160, 190, 80) * (alpha * blink * (0.5f + toxicGlow * 0.5f));
        }

        public void Reset() {
            toxicWavePhase = 0f;
            sulfurPulse = 0f;
            miasmaTimer = 0f;
            bubbles.Clear();
            ashParticles.Clear();
            bubbleTimer = 0;
            ashTimer = 0;
        }

        public void GetParticles(out List<object> particles) {
            particles = [];
            particles.AddRange(ashParticles);
            particles.AddRange(bubbles);
        }

        public void UpdateParticles(Vector2 basePos, float panelFade) {
            //气泡粒子更新
            bubbleTimer++;
            if (panelFade > 0.6f && bubbleTimer >= 12 && bubbles.Count < 15) {
                bubbleTimer = 0;
                Vector2 start = basePos + new Vector2(Main.rand.NextFloat(-80f, 80f), 40f);
                var bb = new BubblePRT(start);
                bb.CoreColor = Color.LightYellow;
                bb.RimColor = Color.LimeGreen;
                bubbles.Add(bb);
            }

            for (int i = bubbles.Count - 1; i >= 0; i--) {
                if (bubbles[i].Update()) {
                    bubbles.RemoveAt(i);
                }
            }

            //灰烬粒子更新
            ashTimer++;
            if (panelFade > 0.6f && ashTimer >= 18 && ashParticles.Count < 10) {
                ashTimer = 0;
                Vector2 start = basePos + new Vector2(Main.rand.NextFloat(-80f, 80f), 40f);
                ashParticles.Add(new AshPRT(start));
            }

            for (int i = ashParticles.Count - 1; i >= 0; i--) {
                if (ashParticles[i].Update()) {
                    ashParticles.RemoveAt(i);
                }
            }
        }

        #region 辅助方法
        private void DrawToxicWaveOverlay(SpriteBatch sb, Rectangle rect, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            int bands = 5;
            for (int i = 0; i < bands; i++) {
                float t = i / (float)bands;
                float y = rect.Y + 18 + t * (rect.Height - 36);
                float amp = 6f + (float)Math.Sin((toxicWavePhase + t) * 2.2f) * 3.5f;
                float thickness = 2.2f;
                int segments = 38;
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

        private static void DrawCornerStar(SpriteBatch sb, Vector2 pos, float a) {
            Texture2D px = VaultAsset.placeholder2.Value;
            float size = 5f;
            Color c = new Color(160, 190, 80) * a;
            sb.Draw(px, pos, new Rectangle(0, 0, 1, 1), c, 0f, new Vector2(0.5f, 0.5f), new Vector2(size, size * 0.26f), SpriteEffects.None, 0f);
            sb.Draw(px, pos, new Rectangle(0, 0, 1, 1), c * 0.8f, MathHelper.PiOver2, new Vector2(0.5f, 0.5f), new Vector2(size, size * 0.26f), SpriteEffects.None, 0f);
        }
        #endregion
    }
}

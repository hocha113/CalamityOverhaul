using CalamityOverhaul.Content.ADV.UIEffect;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.ADV.ADVRewardPopups.Styles
{
    /// <summary>
    /// 海洋风格奖励弹窗
    /// </summary>
    internal class OceanRewardStyle : IRewardPopupStyle
    {
        private float wavePhase = 0f;
        private float abyssPulse = 0f;
        private float panelPulse = 0f;
        private readonly List<BubblePRT> bubbles = new();
        private readonly List<SeaStarPRT> stars = new();
        private int bubbleTimer;
        private int starTimer;

        public void Update(Rectangle panelRect, bool active, bool closing) {
            wavePhase += 0.02f;
            abyssPulse += 0.013f;
            panelPulse += 0.025f;
            if (wavePhase > MathHelper.TwoPi) wavePhase -= MathHelper.TwoPi;
            if (abyssPulse > MathHelper.TwoPi) abyssPulse -= MathHelper.TwoPi;
            if (panelPulse > MathHelper.TwoPi) panelPulse -= MathHelper.TwoPi;
        }

        public void DrawPanel(SpriteBatch spriteBatch, Rectangle rect, float alpha, float hoverGlow) {
            Texture2D px = VaultAsset.placeholder2.Value;

            //深海渐层背景条
            int segs = 26;
            for (int i = 0; i < segs; i++) {
                float t = i / (float)segs;
                float t2 = (i + 1) / (float)segs;
                int y1 = rect.Y + (int)(t * rect.Height);
                int y2 = rect.Y + (int)(t2 * rect.Height);
                int height = Math.Max(1, y2 - y1);

                Rectangle r = new(rect.X, y1, rect.Width, height);

                Color abyssDeep = new Color(2, 10, 18);
                Color abyssMid = new Color(6, 32, 48);
                Color bioEdge = new Color(12, 80, 110);
                float breathing = (float)Math.Sin(abyssPulse) * 0.5f + 0.5f;
                Color blendBase = Color.Lerp(abyssDeep, abyssMid, (float)Math.Sin(panelPulse * 0.5f + t * 1.4f) * 0.5f + 0.5f);
                Color c = Color.Lerp(blendBase, bioEdge, t * 0.55f * (0.4f + breathing * 0.6f));
                c *= alpha * (0.92f + hoverGlow);
                spriteBatch.Draw(px, r, new Rectangle(0, 0, 1, 1), c);
            }

            //波浪横线
            DrawWaveLines(spriteBatch, rect, alpha * 0.65f);

            //内边微光
            Rectangle inner = rect;
            inner.Inflate(-6, -6);
            spriteBatch.Draw(px, inner, new Rectangle(0, 0, 1, 1), new Color(30, 120, 150) * (alpha * (0.08f + hoverGlow * 0.5f) * (0.4f + (float)Math.Sin(panelPulse * 1.3f) * 0.6f)));
        }

        public void DrawFrame(SpriteBatch spriteBatch, Rectangle rect, float alpha, float hoverGlow) {
            Texture2D px = VaultAsset.placeholder2.Value;
            Color edge = new Color(70, 180, 230) * (alpha * (0.85f + hoverGlow * 0.3f));
            spriteBatch.Draw(px, new Rectangle(rect.X, rect.Y, rect.Width, 2), new Rectangle(0, 0, 1, 1), edge);
            spriteBatch.Draw(px, new Rectangle(rect.X, rect.Bottom - 2, rect.Width, 2), new Rectangle(0, 0, 1, 1), edge * 0.7f);
            spriteBatch.Draw(px, new Rectangle(rect.X, rect.Y, 2, rect.Height), new Rectangle(0, 0, 1, 1), edge * 0.85f);
            spriteBatch.Draw(px, new Rectangle(rect.Right - 2, rect.Y, 2, rect.Height), new Rectangle(0, 0, 1, 1), edge * 0.85f);
            DrawCornerStar(spriteBatch, new Vector2(rect.X + 10, rect.Y + 10), alpha * (0.9f + hoverGlow * 0.4f));
            DrawCornerStar(spriteBatch, new Vector2(rect.Right - 10, rect.Y + 10), alpha * (0.9f + hoverGlow * 0.4f));
            DrawCornerStar(spriteBatch, new Vector2(rect.X + 10, rect.Bottom - 10), alpha * (0.6f + hoverGlow * 0.3f));
            DrawCornerStar(spriteBatch, new Vector2(rect.Right - 10, rect.Bottom - 10), alpha * (0.6f + hoverGlow * 0.3f));
        }

        public Color GetNameGlowColor(float alpha) {
            return new Color(140, 230, 255) * (alpha * 0.6f);
        }

        public Color GetNameColor(float alpha) {
            return Color.White * alpha;
        }

        public Color GetHintColor(float alpha, float blink) {
            return new Color(140, 230, 255) * (alpha * blink);
        }

        public void Reset() {
            wavePhase = 0f;
            abyssPulse = 0f;
            panelPulse = 0f;
            bubbles.Clear();
            stars.Clear();
            bubbleTimer = 0;
            starTimer = 0;
        }

        public void GetParticles(out List<object> particles) {
            particles = [.. bubbles, .. stars];
        }

        public void UpdateParticles(Vector2 basePos, float panelFade) {
            bubbleTimer++;
            if (panelFade > 0.6f && bubbleTimer >= 8 && bubbles.Count < 20) {
                bubbleTimer = 0;
                bubbles.Add(new BubblePRT(basePos + new Vector2(Main.rand.NextFloat(-80f, 80f), 40f)));
            }
            starTimer++;
            if (panelFade > 0.6f && starTimer >= 18 && stars.Count < 12) {
                starTimer = 0;
                stars.Add(new SeaStarPRT(basePos + new Vector2(Main.rand.NextFloat(-100f, 100f), Main.rand.NextFloat(-60f, 20f))));
            }
            for (int i = bubbles.Count - 1; i >= 0; i--) if (bubbles[i].Update()) bubbles.RemoveAt(i);
            for (int i = stars.Count - 1; i >= 0; i--) if (stars[i].Update()) stars.RemoveAt(i);
        }

        private void DrawWaveLines(SpriteBatch sb, Rectangle rect, float alpha) {
            Texture2D px = VaultAsset.placeholder2.Value;
            int bands = 4;
            for (int i = 0; i < bands; i++) {
                float t = i / (float)bands;
                float y = rect.Y + 18 + t * (rect.Height - 36);
                float amp = 5f + (float)Math.Sin((wavePhase + t) * 2f) * 3.2f;
                float thickness = 2f;
                int segments = 38;
                Vector2 prev = Vector2.Zero;
                for (int s = 0; s <= segments; s++) {
                    float p = s / (float)segments;
                    float localY = y + (float)Math.Sin(wavePhase * 2f + p * MathHelper.TwoPi * 1.1f + t) * amp;
                    Vector2 point = new(rect.X + 8 + p * (rect.Width - 16), localY);
                    if (s > 0) {
                        Vector2 diff = point - prev;
                        float len = diff.Length();
                        if (len > 0.01f) {
                            float rot = diff.ToRotation();
                            Color c = new Color(30, 120, 170) * (alpha * 0.06f);
                            sb.Draw(px, prev, new Rectangle(0, 0, 1, 1), c, rot, Vector2.Zero, new Vector2(len, thickness), SpriteEffects.None, 0f);
                        }
                    }
                    prev = point;
                }
            }
        }

        private static void DrawCornerStar(SpriteBatch sb, Vector2 pos, float a) {
            Texture2D px = VaultAsset.placeholder2.Value;
            float size = 5f;
            Color c = new Color(150, 230, 255) * a;
            sb.Draw(px, pos, new Rectangle(0, 0, 1, 1), c, 0f, new Vector2(0.5f, 0.5f), new Vector2(size, size * 0.26f), SpriteEffects.None, 0f);
            sb.Draw(px, pos, new Rectangle(0, 0, 1, 1), c * 0.8f, MathHelper.PiOver2, new Vector2(0.5f, 0.5f), new Vector2(size, size * 0.26f), SpriteEffects.None, 0f);
        }
    }
}

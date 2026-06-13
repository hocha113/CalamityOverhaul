using CalamityOverhaul.Content.ADV.UIEffect;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.ADV.ADVRewardPopups.Styles
{
    /// <summary>
    /// 星流风格奖励弹窗
    /// </summary>
    internal class StarStreamRewardStyle : IRewardPopupStyle
    {
        private float starFlowTimer = 0f;
        private float nebulaPulseTimer = 0f;
        private float shimmerTimer = 0f;
        private float constellationPhase = 0f;
        private float auroraTimer = 0f;
        private readonly List<StarStreamPRT> starStreams = new();
        private int starStreamTimer = 0;
        private readonly List<StarDustPRT> starDusts = new();
        private int starDustTimer = 0;
        private const float ParticleMargin = 30f;

        public void Update(Rectangle panelRect, bool active, bool closing) {
            starFlowTimer += 0.04f;
            nebulaPulseTimer += 0.022f;
            shimmerTimer += 0.035f;
            constellationPhase += 0.012f;
            auroraTimer += 0.018f;
            if (starFlowTimer > MathHelper.TwoPi) starFlowTimer -= MathHelper.TwoPi;
            if (nebulaPulseTimer > MathHelper.TwoPi) nebulaPulseTimer -= MathHelper.TwoPi;
            if (shimmerTimer > MathHelper.TwoPi) shimmerTimer -= MathHelper.TwoPi;
            if (constellationPhase > MathHelper.TwoPi) constellationPhase -= MathHelper.TwoPi;
            if (auroraTimer > MathHelper.TwoPi) auroraTimer -= MathHelper.TwoPi;
        }

        public void DrawPanel(SpriteBatch spriteBatch, Rectangle rect, float alpha, float hoverGlow) {
            Texture2D px = VaultAsset.placeholder2.Value;

            // 深空渐变背景
            int segments = 30;
            for (int i = 0; i < segments; i++) {
                float t = i / (float)segments;
                float t2 = (i + 1) / (float)segments;
                int y1 = rect.Y + (int)(t * rect.Height);
                int y2 = rect.Y + (int)(t2 * rect.Height);
                Rectangle r = new(rect.X, y1, rect.Width, Math.Max(1, y2 - y1));

                Color deepSpace = new Color(6, 4, 16);
                Color midSpace = new Color(12, 10, 28);
                Color edgeSpace = new Color(22, 18, 45);

                float nebula = (float)Math.Sin(nebulaPulseTimer * 0.5f + t * 1.8f) * 0.5f + 0.5f;
                Color blendBase = Color.Lerp(deepSpace, midSpace, nebula);
                Color finalColor = Color.Lerp(blendBase, edgeSpace, t * 0.5f);
                finalColor *= alpha * (0.94f + hoverGlow);

                spriteBatch.Draw(px, r, new Rectangle(0, 0, 1, 1), finalColor);
            }

            // 星云呼吸叠加
            float nebulaPulse = (float)Math.Sin(nebulaPulseTimer * 1.3f) * 0.5f + 0.5f;
            Color nebulaOverlay = new Color(30, 15, 50) * (alpha * 0.2f * nebulaPulse);
            spriteBatch.Draw(px, rect, new Rectangle(0, 0, 1, 1), nebulaOverlay);

            // 星座网格
            DrawConstellationGrid(spriteBatch, rect, alpha * 0.7f);

            // 极光光带
            DrawAuroraStreaks(spriteBatch, rect, alpha * 0.6f);

            // 内部金色光晕
            float innerPulse = (float)Math.Sin(shimmerTimer * 1.1f) * 0.5f + 0.5f;
            Rectangle inner = rect;
            inner.Inflate(-5, -5);
            spriteBatch.Draw(px, inner, new Rectangle(0, 0, 1, 1), new Color(200, 160, 60) * (alpha * (0.06f + hoverGlow * 0.4f) * innerPulse));
        }

        public void DrawFrame(SpriteBatch spriteBatch, Rectangle rect, float alpha, float hoverGlow) {
            Texture2D px = VaultAsset.placeholder2.Value;
            float pulse = (float)Math.Sin(shimmerTimer * 1.1f) * 0.5f + 0.5f;

            // 金色外框
            Color outerEdge = Color.Lerp(new Color(180, 140, 50), new Color(240, 200, 100), pulse) * (alpha * (0.8f + hoverGlow * 0.3f));
            spriteBatch.Draw(px, new Rectangle(rect.X, rect.Y, rect.Width, 3), new Rectangle(0, 0, 1, 1), outerEdge);
            spriteBatch.Draw(px, new Rectangle(rect.X, rect.Bottom - 3, rect.Width, 3), new Rectangle(0, 0, 1, 1), outerEdge * 0.7f);
            spriteBatch.Draw(px, new Rectangle(rect.X, rect.Y, 3, rect.Height), new Rectangle(0, 0, 1, 1), outerEdge * 0.9f);
            spriteBatch.Draw(px, new Rectangle(rect.Right - 3, rect.Y, 3, rect.Height), new Rectangle(0, 0, 1, 1), outerEdge * 0.9f);

            // 内框金色光线
            Rectangle inner = rect;
            inner.Inflate(-6, -6);
            Color innerGlow = new Color(255, 220, 120) * (alpha * (0.18f + hoverGlow * 0.5f) * pulse);
            spriteBatch.Draw(px, new Rectangle(inner.X, inner.Y, inner.Width, 1), new Rectangle(0, 0, 1, 1), innerGlow);
            spriteBatch.Draw(px, new Rectangle(inner.X, inner.Bottom - 1, inner.Width, 1), new Rectangle(0, 0, 1, 1), innerGlow * 0.65f);
            spriteBatch.Draw(px, new Rectangle(inner.X, inner.Y, 1, inner.Height), new Rectangle(0, 0, 1, 1), innerGlow * 0.85f);
            spriteBatch.Draw(px, new Rectangle(inner.Right - 1, inner.Y, 1, inner.Height), new Rectangle(0, 0, 1, 1), innerGlow * 0.85f);

            // 顶部流光
            float flowT = (shimmerTimer * 0.8f) % 1f;
            int highlightW = 60;
            int highlightX = rect.X + (int)(flowT * (rect.Width - highlightW));
            Color highlightColor = new Color(255, 230, 140) * (alpha * 0.3f);
            for (int dx = 0; dx < highlightW; dx++) {
                float localT = dx / (float)highlightW;
                float intensity = (float)Math.Sin(localT * MathHelper.Pi);
                spriteBatch.Draw(px, new Rectangle(highlightX + dx, rect.Y, 1, 3), new Rectangle(0, 0, 1, 1), highlightColor * intensity);
            }

            // 角落四芒星
            DrawCornerStar(spriteBatch, new Vector2(rect.X + 12, rect.Y + 12), alpha * (0.95f + hoverGlow * 0.4f));
            DrawCornerStar(spriteBatch, new Vector2(rect.Right - 12, rect.Y + 12), alpha * (0.95f + hoverGlow * 0.4f));
            DrawCornerStar(spriteBatch, new Vector2(rect.X + 12, rect.Bottom - 12), alpha * (0.6f + hoverGlow * 0.3f));
            DrawCornerStar(spriteBatch, new Vector2(rect.Right - 12, rect.Bottom - 12), alpha * (0.6f + hoverGlow * 0.3f));
        }

        public Color GetNameGlowColor(float alpha) {
            return new Color(255, 210, 120) * (alpha * 0.7f);
        }

        public Color GetNameColor(float alpha) {
            return new Color(255, 245, 220) * alpha;
        }

        public Color GetHintColor(float alpha, float blink) {
            return new Color(255, 210, 100) * (alpha * blink);
        }

        public void Reset() {
            starFlowTimer = 0f;
            nebulaPulseTimer = 0f;
            shimmerTimer = 0f;
            constellationPhase = 0f;
            auroraTimer = 0f;
            starStreams.Clear();
            starDusts.Clear();
            starStreamTimer = 0;
            starDustTimer = 0;
        }

        public void GetParticles(out List<object> particles) {
            particles = [.. starStreams, .. starDusts];
        }

        public void UpdateParticles(Vector2 basePos, float panelFade) {
            starStreamTimer++;
            if (panelFade > 0.6f && starStreamTimer >= 14 && starStreams.Count < 15) {
                starStreamTimer = 0;
                float xPos = Main.rand.NextFloat(basePos.X - 100f + ParticleMargin, basePos.X + 100f - ParticleMargin);
                Vector2 startPos = new(xPos, basePos.Y + Main.rand.NextFloat(-50f, 40f));
                starStreams.Add(new StarStreamPRT(startPos));
            }
            for (int i = starStreams.Count - 1; i >= 0; i--) {
                if (starStreams[i].Update(basePos - new Vector2(120, 70), new Vector2(240, 140))) {
                    starStreams.RemoveAt(i);
                }
            }

            starDustTimer++;
            if (panelFade > 0.6f && starDustTimer >= 30 && starDusts.Count < 8) {
                starDustTimer = 0;
                Vector2 startPos = new(
                    Main.rand.NextFloat(basePos.X - 90f, basePos.X + 90f),
                    Main.rand.NextFloat(basePos.Y - 50f, basePos.Y + 50f)
                );
                starDusts.Add(new StarDustPRT(startPos));
            }
            for (int i = starDusts.Count - 1; i >= 0; i--) {
                if (starDusts[i].Update(basePos - new Vector2(120, 70), new Vector2(240, 140))) {
                    starDusts.RemoveAt(i);
                }
            }
        }

        #region 工具方法

        private void DrawConstellationGrid(SpriteBatch sb, Rectangle rect, float alpha) {
            Texture2D px = VaultAsset.placeholder2.Value;
            int rows = 5;
            float rowHeight = rect.Height / (float)rows;

            for (int row = 0; row < rows; row++) {
                float t = row / (float)rows;
                float y = rect.Y + row * rowHeight;
                float phase = constellationPhase + t * MathHelper.Pi;
                float brightness = (float)Math.Sin(phase) * 0.5f + 0.5f;

                Color gridColor = new Color(180, 150, 80) * (alpha * 0.03f * brightness);
                sb.Draw(px, new Rectangle(rect.X + 10, (int)y, rect.Width - 20, 1), new Rectangle(0, 0, 1, 1), gridColor);
            }
        }

        private void DrawAuroraStreaks(SpriteBatch sb, Rectangle rect, float alpha) {
            Texture2D px = VaultAsset.placeholder2.Value;
            int streakCount = 3;

            for (int i = 0; i < streakCount; i++) {
                float t = i / (float)streakCount;
                float baseY = rect.Y + 15 + t * (rect.Height - 30);
                float amplitude = 3f + (float)Math.Sin((auroraTimer + t * 1.5f) * 2f) * 2.5f;

                int segs = 30;
                Vector2 prevPoint = Vector2.Zero;
                for (int s = 0; s <= segs; s++) {
                    float progress = s / (float)segs;
                    float waveY = baseY + (float)Math.Sin(auroraTimer * 2.5f + progress * MathHelper.TwoPi * 1.2f + t * 2.5f) * amplitude;
                    Vector2 point = new(rect.X + 8 + progress * (rect.Width - 16), waveY);

                    if (s > 0) {
                        Vector2 diff = point - prevPoint;
                        float len = diff.Length();
                        if (len > 0.01f) {
                            float rot = diff.ToRotation();
                            Color streakColor = Color.Lerp(new Color(200, 160, 60), new Color(80, 60, 160), progress) * (alpha * 0.05f);
                            sb.Draw(px, prevPoint, new Rectangle(0, 0, 1, 1), streakColor, rot, Vector2.Zero, new Vector2(len, 1.2f), SpriteEffects.None, 0f);
                        }
                    }
                    prevPoint = point;
                }
            }
        }

        private static void DrawCornerStar(SpriteBatch sb, Vector2 pos, float alpha) {
            Texture2D px = VaultAsset.placeholder2.Value;
            float size = 6f;
            Color c = new Color(255, 220, 120) * alpha;

            sb.Draw(px, pos, new Rectangle(0, 0, 1, 1), c, 0f, new Vector2(0.5f, 0.5f), new Vector2(size * 1.3f, size * 0.22f), SpriteEffects.None, 0f);
            sb.Draw(px, pos, new Rectangle(0, 0, 1, 1), c * 0.85f, MathHelper.PiOver2, new Vector2(0.5f, 0.5f), new Vector2(size * 1.3f, size * 0.22f), SpriteEffects.None, 0f);
            sb.Draw(px, pos, new Rectangle(0, 0, 1, 1), c * 0.5f, MathHelper.PiOver4, new Vector2(0.5f, 0.5f), new Vector2(size * 0.8f, size * 0.18f), SpriteEffects.None, 0f);
            sb.Draw(px, pos, new Rectangle(0, 0, 1, 1), c * 0.5f, -MathHelper.PiOver4, new Vector2(0.5f, 0.5f), new Vector2(size * 0.8f, size * 0.18f), SpriteEffects.None, 0f);
            sb.Draw(px, pos, new Rectangle(0, 0, 1, 1), c * 0.7f, 0f, new Vector2(0.5f, 0.5f), new Vector2(size * 0.35f, size * 0.35f), SpriteEffects.None, 0f);
        }

        #endregion
    }
}

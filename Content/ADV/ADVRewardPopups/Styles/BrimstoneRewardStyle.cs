using CalamityOverhaul.Content.ADV.UIEffect;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.ADV.ADVRewardPopups.Styles
{
    /// <summary>
    /// 硫磺火风格奖励弹窗
    /// </summary>
    internal class BrimstoneRewardStyle : IRewardPopupStyle
    {
        private float flameTimer = 0f;
        private float emberGlowTimer = 0f;
        private float heatWavePhase = 0f;
        private float infernoPulse = 0f;
        private readonly List<EmberPRT> embers = new();
        private int emberSpawnTimer = 0;
        private readonly List<AshPRT> ashes = new();
        private int ashSpawnTimer = 0;
        private readonly List<FlameWispPRT> flameWisps = new();
        private int wispSpawnTimer = 0;
        private const float ParticleSideMargin = 30f;

        public void Update(Rectangle panelRect, bool active, bool closing) {
            flameTimer += 0.045f;
            emberGlowTimer += 0.038f;
            heatWavePhase += 0.025f;
            infernoPulse += 0.012f;
            if (flameTimer > MathHelper.TwoPi) flameTimer -= MathHelper.TwoPi;
            if (emberGlowTimer > MathHelper.TwoPi) emberGlowTimer -= MathHelper.TwoPi;
            if (heatWavePhase > MathHelper.TwoPi) heatWavePhase -= MathHelper.TwoPi;
            if (infernoPulse > MathHelper.TwoPi) infernoPulse -= MathHelper.TwoPi;
        }

        public void DrawPanel(SpriteBatch spriteBatch, Rectangle rect, float alpha, float hoverGlow) {
            Texture2D px = VaultAsset.placeholder2.Value;

            //渐变背景 - 硫磺火深红色
            int segments = 35;
            for (int i = 0; i < segments; i++) {
                float t = i / (float)segments;
                float t2 = (i + 1) / (float)segments;
                int y1 = rect.Y + (int)(t * rect.Height);
                int y2 = rect.Y + (int)(t2 * rect.Height);
                Rectangle r = new(rect.X, y1, rect.Width, Math.Max(1, y2 - y1));

                Color brimstoneDeep = new Color(25, 5, 5);
                Color brimstoneMid = new Color(80, 15, 10);
                Color brimstoneHot = new Color(140, 35, 20);

                float breathing = (float)Math.Sin(infernoPulse * 1.5f) * 0.5f + 0.5f;
                float flameWave = (float)Math.Sin(flameTimer * 0.6f + t * 2.2f) * 0.5f + 0.5f;

                Color baseColor = Color.Lerp(brimstoneDeep, brimstoneMid, flameWave);
                Color finalColor = Color.Lerp(baseColor, brimstoneHot, t * 0.5f * (0.3f + breathing * 0.7f));
                finalColor *= alpha * (0.92f + hoverGlow);

                spriteBatch.Draw(px, r, new Rectangle(0, 0, 1, 1), finalColor);
            }

            //火焰脉冲叠加层
            float pulseBrightness = (float)Math.Sin(infernoPulse * 1.8f) * 0.5f + 0.5f;
            Color pulseOverlay = new Color(120, 25, 15) * (alpha * 0.25f * pulseBrightness);
            spriteBatch.Draw(px, rect, new Rectangle(0, 0, 1, 1), pulseOverlay);

            //热浪扭曲效果层
            DrawHeatWave(spriteBatch, rect, alpha * 0.85f);

            //内发光
            float glowPulse = (float)Math.Sin(emberGlowTimer * 1.5f) * 0.5f + 0.5f;
            Rectangle inner = rect;
            inner.Inflate(-7, -7);
            spriteBatch.Draw(px, inner, new Rectangle(0, 0, 1, 1), new Color(180, 60, 30) * (alpha * (0.12f + hoverGlow * 0.5f) * (0.5f + glowPulse * 0.5f)));
        }

        public void DrawFrame(SpriteBatch spriteBatch, Rectangle rect, float alpha, float hoverGlow) {
            Texture2D px = VaultAsset.placeholder2.Value;
            float pulse = (float)Math.Sin(emberGlowTimer * 1.5f) * 0.5f + 0.5f;

            Color outerEdge = Color.Lerp(new Color(180, 60, 30), new Color(255, 140, 70), pulse) * (alpha * (0.85f + hoverGlow * 0.3f));
            spriteBatch.Draw(px, new Rectangle(rect.X, rect.Y, rect.Width, 3), new Rectangle(0, 0, 1, 1), outerEdge);
            spriteBatch.Draw(px, new Rectangle(rect.X, rect.Bottom - 3, rect.Width, 3), new Rectangle(0, 0, 1, 1), outerEdge * 0.75f);
            spriteBatch.Draw(px, new Rectangle(rect.X, rect.Y, 3, rect.Height), new Rectangle(0, 0, 1, 1), outerEdge * 0.9f);
            spriteBatch.Draw(px, new Rectangle(rect.Right - 3, rect.Y, 3, rect.Height), new Rectangle(0, 0, 1, 1), outerEdge * 0.9f);

            Rectangle inner = rect;
            inner.Inflate(-6, -6);
            Color innerGlow = new Color(220, 100, 50) * (alpha * (0.22f + hoverGlow * 0.5f) * pulse);
            spriteBatch.Draw(px, new Rectangle(inner.X, inner.Y, inner.Width, 1), new Rectangle(0, 0, 1, 1), innerGlow);
            spriteBatch.Draw(px, new Rectangle(inner.X, inner.Bottom - 1, inner.Width, 1), new Rectangle(0, 0, 1, 1), innerGlow * 0.7f);
            spriteBatch.Draw(px, new Rectangle(inner.X, inner.Y, 1, inner.Height), new Rectangle(0, 0, 1, 1), innerGlow * 0.85f);
            spriteBatch.Draw(px, new Rectangle(inner.Right - 1, inner.Y, 1, inner.Height), new Rectangle(0, 0, 1, 1), innerGlow * 0.85f);

            DrawFlameMark(spriteBatch, new Vector2(rect.X + 12, rect.Y + 12), alpha * (0.95f + hoverGlow * 0.4f));
            DrawFlameMark(spriteBatch, new Vector2(rect.Right - 12, rect.Y + 12), alpha * (0.95f + hoverGlow * 0.4f));
            DrawFlameMark(spriteBatch, new Vector2(rect.X + 12, rect.Bottom - 12), alpha * (0.65f + hoverGlow * 0.3f));
            DrawFlameMark(spriteBatch, new Vector2(rect.Right - 12, rect.Bottom - 12), alpha * (0.65f + hoverGlow * 0.3f));
        }

        public Color GetNameGlowColor(float alpha) {
            return new Color(255, 150, 80) * (alpha * 0.6f);
        }

        public Color GetNameColor(float alpha) {
            return new Color(255, 220, 200) * alpha;
        }

        public Color GetHintColor(float alpha, float blink) {
            return new Color(255, 160, 90) * (alpha * blink);
        }

        public void Reset() {
            flameTimer = 0f;
            emberGlowTimer = 0f;
            heatWavePhase = 0f;
            infernoPulse = 0f;
            embers.Clear();
            ashes.Clear();
            flameWisps.Clear();
            emberSpawnTimer = 0;
            ashSpawnTimer = 0;
            wispSpawnTimer = 0;
        }

        public void GetParticles(out List<object> particles) {
            particles = [.. ashes, .. flameWisps, .. embers];//想出这个语法糖的人真你麻痹是个天才
        }

        public void UpdateParticles(Vector2 basePos, float panelFade) {
            emberSpawnTimer++;
            if (panelFade > 0.6f && emberSpawnTimer >= 8 && embers.Count < 35) {
                emberSpawnTimer = 0;
                float xPos = Main.rand.NextFloat(basePos.X - 120f + ParticleSideMargin, basePos.X + 120f - ParticleSideMargin);
                Vector2 startPos = new(xPos, basePos.Y + 66f - 5f);
                embers.Add(new EmberPRT(startPos));
            }
            for (int i = embers.Count - 1; i >= 0; i--) {
                if (embers[i].Update(basePos)) {
                    embers.RemoveAt(i);
                }
            }

            ashSpawnTimer++;
            if (panelFade > 0.6f && ashSpawnTimer >= 12 && ashes.Count < 25) {
                ashSpawnTimer = 0;
                float xPos = Main.rand.NextFloat(basePos.X - 120f + ParticleSideMargin, basePos.X + 120f - ParticleSideMargin);
                Vector2 startPos = new(xPos, basePos.Y + 66f);
                ashes.Add(new AshPRT(startPos));
            }
            for (int i = ashes.Count - 1; i >= 0; i--) {
                if (ashes[i].Update(basePos)) {
                    ashes.RemoveAt(i);
                }
            }

            wispSpawnTimer++;
            if (panelFade > 0.6f && wispSpawnTimer >= 45 && flameWisps.Count < 8) {
                wispSpawnTimer = 0;
                Vector2 startPos = new(
                    Main.rand.NextFloat(basePos.X - 80f, basePos.X + 80f),
                    Main.rand.NextFloat(basePos.Y - 40f, basePos.Y + 40f)
                );
                var fw = new FlameWispPRT(startPos);
                fw.Size = Main.rand.NextFloat(8f, 12f);
                flameWisps.Add(fw);
            }
            for (int i = flameWisps.Count - 1; i >= 0; i--) {
                if (flameWisps[i].Update(basePos)) {
                    flameWisps.RemoveAt(i);
                }
            }
        }

        private void DrawHeatWave(SpriteBatch sb, Rectangle rect, float alpha) {
            Texture2D px = VaultAsset.placeholder2.Value;
            int waveCount = 8;
            for (int i = 0; i < waveCount; i++) {
                float t = i / (float)waveCount;
                float baseY = rect.Y + 25 + t * (rect.Height - 50);
                float amplitude = 5f + (float)Math.Sin((heatWavePhase + t * 1.2f) * 2.5f) * 3.5f;
                float thickness = 1.8f;

                int segments = 50;
                Vector2 prevPoint = Vector2.Zero;
                for (int s = 0; s <= segments; s++) {
                    float progress = s / (float)segments;
                    float waveY = baseY + (float)Math.Sin(heatWavePhase * 3f + progress * MathHelper.TwoPi * 1.5f + t * 2f) * amplitude;
                    Vector2 point = new(rect.X + 12 + progress * (rect.Width - 24), waveY);

                    if (s > 0) {
                        Vector2 diff = point - prevPoint;
                        float len = diff.Length();
                        if (len > 0.01f) {
                            float rot = diff.ToRotation();
                            Color waveColor = new Color(180, 60, 30) * (alpha * 0.08f);
                            sb.Draw(px, prevPoint, new Rectangle(0, 0, 1, 1), waveColor, rot, Vector2.Zero, new Vector2(len, thickness), SpriteEffects.None, 0f);
                        }
                    }
                    prevPoint = point;
                }
            }
        }

        private static void DrawFlameMark(SpriteBatch sb, Vector2 pos, float alpha) {
            Texture2D px = VaultAsset.placeholder2.Value;
            float size = 6f;
            Color flameColor = new Color(255, 150, 70) * alpha;

            sb.Draw(px, pos, new Rectangle(0, 0, 1, 1), flameColor, 0f, new Vector2(0.5f, 0.5f), new Vector2(size * 1.2f, size * 0.3f), SpriteEffects.None, 0f);
            sb.Draw(px, pos, new Rectangle(0, 0, 1, 1), flameColor * 0.85f, MathHelper.PiOver2, new Vector2(0.5f, 0.5f), new Vector2(size * 1.2f, size * 0.3f), SpriteEffects.None, 0f);
            sb.Draw(px, pos, new Rectangle(0, 0, 1, 1), flameColor * 0.7f, MathHelper.PiOver4, new Vector2(0.5f, 0.5f), new Vector2(size * 0.9f, size * 0.25f), SpriteEffects.None, 0f);
            sb.Draw(px, pos, new Rectangle(0, 0, 1, 1), flameColor * 0.7f, -MathHelper.PiOver4, new Vector2(0.5f, 0.5f), new Vector2(size * 0.9f, size * 0.25f), SpriteEffects.None, 0f);
        }
    }
}

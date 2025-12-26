using CalamityOverhaul.Content.ADV.UIEffect;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.ADV.DialogueBoxs
{
    /// <summary>
    /// 至尊灾厄硫磺火风格对话框
    /// </summary>
    internal class BrimstoneDialogueBox : DialogueBoxBase
    {
        public static BrimstoneDialogueBox Instance => UIHandleLoader.GetUIHandleOfType<BrimstoneDialogueBox>();
        public override string LocalizationCategory => "UI";

        //风格参数
        private const float FixedWidth = 540f;
        protected override float PanelWidth => FixedWidth;

        //火焰动画参数
        private float flameTimer = 0f;
        private float emberGlowTimer = 0f;
        private float heatWavePhase = 0f;
        private float infernoPulse = 0f;

        //粒子系统
        private readonly List<EmberPRT> embers = new();
        private int emberSpawnTimer = 0;
        private readonly List<AshPRT> ashes = new();
        private int ashSpawnTimer = 0;
        private readonly List<FlameWispPRT> flameWisps = new();
        private int wispSpawnTimer = 0;
        private const float ParticleSideMargin = 30f;

        #region 样式配置重写

        protected override float PortraitScaleMin => 0.82f;
        protected override float TopNameOffsetBase => 12f;
        protected override float TextBlockOffsetBase => 38f;
        protected override int NameGlowCount => 6;
        protected override float NameGlowRadius => 2.2f;

        protected override Color GetSilhouetteColor(ContentDrawContext ctx) => new Color(40, 10, 5) * 0.85f;

        protected override Color GetTextLineColor(ContentDrawContext ctx, int lineIndex) {
            return Color.Lerp(new Color(255, 220, 200), new Color(255, 240, 220), 0.4f) * ctx.ContentAlpha;
        }

        protected override Vector2 ApplyTextLineOffset(ContentDrawContext ctx, Vector2 basePosition, int lineIndex) {
            float heatWobble = (float)Math.Sin(heatWavePhase * 3f + lineIndex * 0.65f) * 0.9f;
            return basePosition + new Vector2(heatWobble, 0);
        }

        protected override void DrawTextLineGlow(ContentDrawContext ctx, string text, Vector2 position, int lineIndex) {
            Color textGlow = new Color(255, 150, 80) * (ctx.ContentAlpha * 0.15f);
            Utils.DrawBorderString(ctx.SpriteBatch, text, position + new Vector2(0, 1), textGlow, TextScale);
        }

        protected override string GetContinueHintText() => $"▶ {ContinueHint.Value} ◀";

        protected override Color GetContinueHintColor(ContentDrawContext ctx, float blink) {
            return new Color(255, 160, 90) * blink * ctx.ContentAlpha;
        }

        protected override Color GetFastHintColor(ContentDrawContext ctx) {
            return new Color(200, 140, 100) * 0.45f * ctx.ContentAlpha;
        }

        #endregion

        #region 抽象方法实现

        protected override void DrawPortraitFrame(ContentDrawContext ctx, Rectangle frameRect) {
            Texture2D vaule = VaultAsset.placeholder2.Value;
            float alpha = ctx.Alpha * ctx.PortraitData.Fade * ctx.PortraitExtraAlpha;

            Color back = new Color(20, 5, 5) * (alpha * 0.88f);
            ctx.SpriteBatch.Draw(vaule, frameRect, new Rectangle(0, 0, 1, 1), back);

            Color edge = new Color(200, 80, 40) * (alpha * 0.75f);
            ctx.SpriteBatch.Draw(vaule, new Rectangle(frameRect.X, frameRect.Y, frameRect.Width, 3), new Rectangle(0, 0, 1, 1), edge);
            ctx.SpriteBatch.Draw(vaule, new Rectangle(frameRect.X, frameRect.Bottom - 3, frameRect.Width, 3), new Rectangle(0, 0, 1, 1), edge * 0.7f);
            ctx.SpriteBatch.Draw(vaule, new Rectangle(frameRect.X, frameRect.Y, 3, frameRect.Height), new Rectangle(0, 0, 1, 1), edge * 0.85f);
            ctx.SpriteBatch.Draw(vaule, new Rectangle(frameRect.Right - 3, frameRect.Y, 3, frameRect.Height), new Rectangle(0, 0, 1, 1), edge * 0.85f);
        }

        protected override void DrawPortraitGlow(ContentDrawContext ctx, Rectangle glowRect) {
            var pd = ctx.PortraitData;
            float flamePulse = (float)Math.Sin(flameTimer * 1.8f + pd.Fade) * 0.5f + 0.5f;
            Color flameRim = new Color(255, 120, 60) * (ctx.ContentAlpha * 0.5f * flamePulse * pd.Fade) * ctx.PortraitExtraAlpha;
            DrawFlameGlow(ctx.SpriteBatch, glowRect, flameRim);
        }

        protected override void DrawNameGlow(ContentDrawContext ctx, Vector2 position, float alpha) {
            Color nameGlow = new Color(255, 140, 80) * alpha * 0.75f;
            for (int i = 0; i < NameGlowCount; i++) {
                float angle = MathHelper.TwoPi * i / NameGlowCount + flameTimer * 0.5f;
                Vector2 offset = angle.ToRotationVector2() * NameGlowRadius * ctx.SwitchEase;
                Utils.DrawBorderString(ctx.SpriteBatch, current.Speaker, position + offset, nameGlow * 0.5f, NameScale);
            }
        }

        protected override void DrawSpeakerName(ContentDrawContext ctx) {
            Vector2 speakerPos = GetSpeakerNamePosition(ctx);
            float nameAlpha = ctx.ContentAlpha * ctx.SwitchEase;

            DrawNameGlow(ctx, speakerPos, nameAlpha);
            Utils.DrawBorderString(ctx.SpriteBatch, current.Speaker, speakerPos, new Color(255, 240, 220) * nameAlpha, NameScale);

            Vector2 divStart = speakerPos + new Vector2(0, 28);
            Vector2 divEnd = divStart + new Vector2(ctx.PanelRect.Width - ctx.LeftOffset - Padding, 0);
            DrawDividerLine(ctx, divStart, divEnd, nameAlpha);
        }

        protected override void DrawDividerLine(ContentDrawContext ctx, Vector2 start, Vector2 end, float alpha) {
            DrawFlameGradientLine(ctx.SpriteBatch, start, end,
                new Color(220, 80, 40) * (alpha * 0.9f),
                new Color(120, 30, 15) * (alpha * 0.1f), 1.5f);
        }

        #endregion

        protected override void StyleUpdate(Vector2 panelPos, Vector2 panelSize) {
            flameTimer += 0.045f;
            emberGlowTimer += 0.038f;
            heatWavePhase += 0.025f;
            infernoPulse += 0.012f;
            if (flameTimer > MathHelper.TwoPi) flameTimer -= MathHelper.TwoPi;
            if (emberGlowTimer > MathHelper.TwoPi) emberGlowTimer -= MathHelper.TwoPi;
            if (heatWavePhase > MathHelper.TwoPi) heatWavePhase -= MathHelper.TwoPi;
            if (infernoPulse > MathHelper.TwoPi) infernoPulse -= MathHelper.TwoPi;

            emberSpawnTimer++;
            if (Active && emberSpawnTimer >= 8 && embers.Count < 35) {
                emberSpawnTimer = 0;
                float xPos = Main.rand.NextFloat(panelPos.X + ParticleSideMargin, panelPos.X + panelSize.X - ParticleSideMargin);
                Vector2 startPos = new(xPos, panelPos.Y + panelSize.Y - 5f);
                embers.Add(new EmberPRT(startPos));
            }
            for (int i = embers.Count - 1; i >= 0; i--) {
                if (embers[i].Update(panelPos, panelSize)) {
                    embers.RemoveAt(i);
                }
            }

            ashSpawnTimer++;
            if (Active && ashSpawnTimer >= 12 && ashes.Count < 25) {
                ashSpawnTimer = 0;
                float xPos = Main.rand.NextFloat(panelPos.X + ParticleSideMargin, panelPos.X + panelSize.X - ParticleSideMargin);
                Vector2 startPos = new(xPos, panelPos.Y + panelSize.Y);
                ashes.Add(new AshPRT(startPos));
            }
            for (int i = ashes.Count - 1; i >= 0; i--) {
                if (ashes[i].Update(panelPos, panelSize)) {
                    ashes.RemoveAt(i);
                }
            }

            wispSpawnTimer++;
            if (Active && wispSpawnTimer >= 45 && flameWisps.Count < 8) {
                wispSpawnTimer = 0;
                Vector2 startPos = new(
                    Main.rand.NextFloat(panelPos.X + 40f, panelPos.X + panelSize.X - 40f),
                    Main.rand.NextFloat(panelPos.Y + 60f, panelPos.Y + panelSize.Y - 60f)
                );
                var prt = new FlameWispPRT(startPos);
                prt.Size = Main.rand.NextFloat(8f, 12f);
                flameWisps.Add(prt);
            }
            for (int i = flameWisps.Count - 1; i >= 0; i--) {
                if (flameWisps[i].Update(panelPos, panelSize)) {
                    flameWisps.RemoveAt(i);
                }
            }
        }

        protected override void DrawStyle(SpriteBatch spriteBatch, Rectangle panelRect, float alpha, float contentAlpha, float easedProgress) {
            Texture2D vaule = VaultAsset.placeholder2.Value;

            Rectangle shadow = panelRect;
            shadow.Offset(7, 9);
            spriteBatch.Draw(vaule, shadow, new Rectangle(0, 0, 1, 1), new Color(20, 0, 0) * (alpha * 0.65f));

            int segments = 35;
            for (int i = 0; i < segments; i++) {
                float t = i / (float)segments;
                float t2 = (i + 1) / (float)segments;
                int y1 = panelRect.Y + (int)(t * panelRect.Height);
                int y2 = panelRect.Y + (int)(t2 * panelRect.Height);
                Rectangle r = new(panelRect.X, y1, panelRect.Width, Math.Max(1, y2 - y1));

                Color brimstoneDeep = new Color(25, 5, 5);
                Color brimstoneMid = new Color(80, 15, 10);
                Color brimstoneHot = new Color(140, 35, 20);

                float breathing = (float)Math.Sin(infernoPulse * 1.5f) * 0.5f + 0.5f;
                float flameWave = (float)Math.Sin(flameTimer * 0.6f + t * 2.2f) * 0.5f + 0.5f;

                Color baseColor = Color.Lerp(brimstoneDeep, brimstoneMid, flameWave);
                Color finalColor = Color.Lerp(baseColor, brimstoneHot, t * 0.5f * (0.3f + breathing * 0.7f));
                finalColor *= alpha * 0.92f;

                spriteBatch.Draw(vaule, r, new Rectangle(0, 0, 1, 1), finalColor);
            }

            float pulseBrightness = (float)Math.Sin(infernoPulse * 1.8f) * 0.5f + 0.5f;
            Color pulseOverlay = new Color(120, 25, 15) * (alpha * 0.25f * pulseBrightness);
            spriteBatch.Draw(vaule, panelRect, new Rectangle(0, 0, 1, 1), pulseOverlay);

            DrawHeatWaveOverlay(spriteBatch, panelRect, alpha * 0.85f);

            float glowPulse = (float)Math.Sin(emberGlowTimer * 1.5f) * 0.5f + 0.5f;
            Rectangle inner = panelRect;
            inner.Inflate(-7, -7);
            spriteBatch.Draw(vaule, inner, new Rectangle(0, 0, 1, 1), new Color(180, 60, 30) * (alpha * 0.12f * (0.5f + glowPulse * 0.5f)));

            DrawBrimstoneFrame(spriteBatch, panelRect, alpha, glowPulse);

            foreach (var ash in ashes) {
                ash.Draw(spriteBatch, alpha * 0.7f);
            }
            foreach (var wisp in flameWisps) {
                wisp.Draw(spriteBatch, alpha * 0.8f);
            }
            foreach (var ember in embers) {
                ember.Draw(spriteBatch, alpha * 0.95f);
            }

            if (current == null || contentAlpha <= 0.01f) {
                return;
            }

            DrawPortraitAndText(spriteBatch, panelRect, alpha, contentAlpha);
        }

        #region 样式工具函数
        private void DrawHeatWaveOverlay(SpriteBatch sb, Rectangle rect, float alpha) {
            Texture2D vaule = VaultAsset.placeholder2.Value;
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
                            sb.Draw(vaule, prevPoint, new Rectangle(0, 0, 1, 1), waveColor, rot, Vector2.Zero, new Vector2(len, thickness), SpriteEffects.None, 0f);
                        }
                    }
                    prevPoint = point;
                }
            }
        }

        private static void DrawBrimstoneFrame(SpriteBatch sb, Rectangle rect, float alpha, float pulse) {
            Texture2D vaule = VaultAsset.placeholder2.Value;

            Color outerEdge = Color.Lerp(new Color(180, 60, 30), new Color(255, 140, 70), pulse) * (alpha * 0.85f);
            sb.Draw(vaule, new Rectangle(rect.X, rect.Y, rect.Width, 3), new Rectangle(0, 0, 1, 1), outerEdge);
            sb.Draw(vaule, new Rectangle(rect.X, rect.Bottom - 3, rect.Width, 3), new Rectangle(0, 0, 1, 1), outerEdge * 0.75f);
            sb.Draw(vaule, new Rectangle(rect.X, rect.Y, 3, rect.Height), new Rectangle(0, 0, 1, 1), outerEdge * 0.9f);
            sb.Draw(vaule, new Rectangle(rect.Right - 3, rect.Y, 3, rect.Height), new Rectangle(0, 0, 1, 1), outerEdge * 0.9f);

            Rectangle inner = rect;
            inner.Inflate(-6, -6);
            Color innerGlow = new Color(220, 100, 50) * (alpha * 0.22f * pulse);
            sb.Draw(vaule, new Rectangle(inner.X, inner.Y, inner.Width, 1), new Rectangle(0, 0, 1, 1), innerGlow);
            sb.Draw(vaule, new Rectangle(inner.X, inner.Bottom - 1, inner.Width, 1), new Rectangle(0, 0, 1, 1), innerGlow * 0.7f);
            sb.Draw(vaule, new Rectangle(inner.X, inner.Y, 1, inner.Height), new Rectangle(0, 0, 1, 1), innerGlow * 0.85f);
            sb.Draw(vaule, new Rectangle(inner.Right - 1, inner.Y, 1, inner.Height), new Rectangle(0, 0, 1, 1), innerGlow * 0.85f);

            DrawFlameMark(sb, new Vector2(rect.X + 12, rect.Y + 12), alpha * 0.95f);
            DrawFlameMark(sb, new Vector2(rect.Right - 12, rect.Y + 12), alpha * 0.95f);
            DrawFlameMark(sb, new Vector2(rect.X + 12, rect.Bottom - 12), alpha * 0.65f);
            DrawFlameMark(sb, new Vector2(rect.Right - 12, rect.Bottom - 12), alpha * 0.65f);
        }

        private static void DrawFlameMark(SpriteBatch sb, Vector2 pos, float alpha) {
            Texture2D vaule = VaultAsset.placeholder2.Value;
            float size = 6f;
            Color flameColor = new Color(255, 150, 70) * alpha;

            sb.Draw(vaule, pos, new Rectangle(0, 0, 1, 1), flameColor, 0f, new Vector2(0.5f, 0.5f), new Vector2(size * 1.2f, size * 0.3f), SpriteEffects.None, 0f);
            sb.Draw(vaule, pos, new Rectangle(0, 0, 1, 1), flameColor * 0.85f, MathHelper.PiOver2, new Vector2(0.5f, 0.5f), new Vector2(size * 1.2f, size * 0.3f), SpriteEffects.None, 0f);
            sb.Draw(vaule, pos, new Rectangle(0, 0, 1, 1), flameColor * 0.7f, MathHelper.PiOver4, new Vector2(0.5f, 0.5f), new Vector2(size * 0.9f, size * 0.25f), SpriteEffects.None, 0f);
            sb.Draw(vaule, pos, new Rectangle(0, 0, 1, 1), flameColor * 0.7f, -MathHelper.PiOver4, new Vector2(0.5f, 0.5f), new Vector2(size * 0.9f, size * 0.25f), SpriteEffects.None, 0f);
        }

        private static void DrawFlameGradientLine(SpriteBatch spriteBatch, Vector2 start, Vector2 end, Color startColor, Color endColor, float thickness) {
            Texture2D vaule = VaultAsset.placeholder2.Value;
            Vector2 edge = end - start;
            float length = edge.Length();
            if (length < 1f) {
                return;
            }
            edge.Normalize();
            float rotation = (float)Math.Atan2(edge.Y, edge.X);
            int segments = Math.Max(1, (int)(length / 10f));
            for (int i = 0; i < segments; i++) {
                float t = i / (float)segments;
                Vector2 segPos = start + edge * (length * t);
                float segLength = length / segments;
                Color color = Color.Lerp(startColor, endColor, t);
                spriteBatch.Draw(vaule, segPos, new Rectangle(0, 0, 1, 1), color, rotation, new Vector2(0, 0.5f), new Vector2(segLength, thickness), SpriteEffects.None, 0);
            }
        }

        private static void DrawFlameGlow(SpriteBatch sb, Rectangle rect, Color glow) {
            Texture2D vaule = VaultAsset.placeholder2.Value;

            sb.Draw(vaule, rect, new Rectangle(0, 0, 1, 1), glow * 0.2f);

            int border = 2;
            sb.Draw(vaule, new Rectangle(rect.X, rect.Y, rect.Width, border), new Rectangle(0, 0, 1, 1), glow * 0.7f);
            sb.Draw(vaule, new Rectangle(rect.X, rect.Bottom - border, rect.Width, border), new Rectangle(0, 0, 1, 1), glow * 0.5f);
            sb.Draw(vaule, new Rectangle(rect.X, rect.Y, border, rect.Height), new Rectangle(0, 0, 1, 1), glow * 0.6f);
            sb.Draw(vaule, new Rectangle(rect.Right - border, rect.Y, border, rect.Height), new Rectangle(0, 0, 1, 1), glow * 0.6f);
        }
        #endregion
    }
}

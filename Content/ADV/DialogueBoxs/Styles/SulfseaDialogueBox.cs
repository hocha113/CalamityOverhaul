using CalamityOverhaul.Content.ADV.UIEffect;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.ADV.DialogueBoxs.Styles
{
    /// <summary>
    /// 硫磺海风格 GalGame 对话框
    /// </summary>
    internal class SulfseaDialogueBox : DialogueBoxBase
    {
        public static SulfseaDialogueBox Instance => UIHandleLoader.GetUIHandleOfType<SulfseaDialogueBox>();
        public override string LocalizationCategory => "UI";

        //风格参数
        private const float FixedWidth = 520f;
        protected override float PanelWidth => FixedWidth;

        //背景动画参数
        private float panelPulseTimer = 0f;
        private float scanTimer = 0f;
        private float toxicWavePhase = 0f;
        private float sulfurPulse = 0f;
        private float miasmaTimer = 0f;

        //视觉粒子
        private readonly List<SeaStarPRT> starFx = [];
        private int starSpawnTimer = 0;
        private readonly List<BubblePRT> bubbles = [];
        private int bubbleSpawnTimer = 0;
        private const float BubbleSideMargin = 34f;

        //硫磺特效粒子
        private readonly List<AshPRT> ashParticles = [];
        private int ashSpawnTimer = 0;

        #region 样式配置重写

        protected override Color GetSilhouetteColor(ContentDrawContext ctx) => new Color(20, 30, 15) * 0.85f;

        protected override Color GetTextLineColor(ContentDrawContext ctx, int lineIndex) {
            return Color.Lerp(new Color(200, 210, 150), Color.White, 0.3f) * ctx.ContentAlpha;
        }

        protected override Vector2 ApplyTextLineOffset(ContentDrawContext ctx, Vector2 basePosition, int lineIndex) {
            float wobble = (float)Math.Sin(toxicWavePhase * 2.4f + lineIndex * 0.6f) * 1.3f;
            return basePosition + new Vector2(wobble, 0);
        }

        protected override Color GetContinueHintColor(ContentDrawContext ctx, float blink) {
            return new Color(160, 190, 80) * blink * ctx.ContentAlpha;
        }

        protected override Color GetFastHintColor(ContentDrawContext ctx) {
            return new Color(140, 170, 75) * 0.45f * ctx.ContentAlpha;
        }

        #endregion

        #region 模板方法实现

        protected override void DrawPortraitFrame(ContentDrawContext ctx, Rectangle frameRect) {
            Texture2D vaule = VaultAsset.placeholder2.Value;
            float alpha = ctx.Alpha * ctx.PortraitData.Fade * ctx.PortraitExtraAlpha;

            Color back = new Color(10, 15, 8) * (alpha * 0.88f);
            ctx.SpriteBatch.Draw(vaule, frameRect, new Rectangle(0, 0, 1, 1), back);

            Color edge = new Color(100, 130, 50) * (alpha * 0.65f);
            ctx.SpriteBatch.Draw(vaule, new Rectangle(frameRect.X, frameRect.Y, frameRect.Width, 2), new Rectangle(0, 0, 1, 1), edge);
            ctx.SpriteBatch.Draw(vaule, new Rectangle(frameRect.X, frameRect.Bottom - 2, frameRect.Width, 2), new Rectangle(0, 0, 1, 1), edge * 0.75f);
            ctx.SpriteBatch.Draw(vaule, new Rectangle(frameRect.X, frameRect.Y, 2, frameRect.Height), new Rectangle(0, 0, 1, 1), edge * 0.85f);
            ctx.SpriteBatch.Draw(vaule, new Rectangle(frameRect.Right - 2, frameRect.Y, 2, frameRect.Height), new Rectangle(0, 0, 1, 1), edge * 0.85f);
        }

        protected override void DrawPortraitGlow(ContentDrawContext ctx, Rectangle glowRect) {
            var pd = ctx.PortraitData;
            Color rim = new Color(140, 180, 70) * (ctx.ContentAlpha * 0.45f * (float)Math.Sin(panelPulseTimer * 1.3f + pd.Fade) * pd.Fade + 0.5f * pd.Fade) * ctx.PortraitExtraAlpha;
            DrawSulfseaGlowRect(ctx.SpriteBatch, glowRect, rim);
        }

        protected override void DrawNameGlow(ContentDrawContext ctx, Vector2 position, float alpha) {
            Color nameGlow = new Color(160, 190, 80) * alpha * 0.75f;
            for (int i = 0; i < NameGlowCount; i++) {
                float a = MathHelper.TwoPi * i / NameGlowCount;
                Vector2 off = a.ToRotationVector2() * NameGlowRadius * ctx.SwitchEase;
                Utils.DrawBorderString(ctx.SpriteBatch, current.Speaker, position + off, nameGlow * 0.6f, NameScale);
            }
        }

        protected override void DrawDividerLine(ContentDrawContext ctx, Vector2 start, Vector2 end, float alpha) {
            DrawGradientLine(ctx.SpriteBatch, start, end,
                new Color(100, 140, 50) * (alpha * 0.9f),
                new Color(100, 140, 50) * (alpha * 0.08f),
                DividerLineThickness);
        }

        #endregion

        protected override void StyleUpdate(Vector2 panelPos, Vector2 panelSize) {
            panelPulseTimer += 0.028f;
            scanTimer += 0.018f;
            toxicWavePhase += 0.022f;
            sulfurPulse += 0.015f;
            miasmaTimer += 0.032f;
            if (panelPulseTimer > MathHelper.TwoPi) panelPulseTimer -= MathHelper.TwoPi;
            if (scanTimer > MathHelper.TwoPi) scanTimer -= MathHelper.TwoPi;
            if (toxicWavePhase > MathHelper.TwoPi) toxicWavePhase -= MathHelper.TwoPi;
            if (sulfurPulse > MathHelper.TwoPi) sulfurPulse -= MathHelper.TwoPi;
            if (miasmaTimer > MathHelper.TwoPi) miasmaTimer -= MathHelper.TwoPi;

            starSpawnTimer++;
            if (Active && starSpawnTimer >= 35 && starFx.Count < 8) {
                starSpawnTimer = 0;
                Vector2 p = panelPos + new Vector2(Main.rand.NextFloat(BubbleSideMargin, panelSize.X - BubbleSideMargin), Main.rand.NextFloat(56f, panelSize.Y - 56f));
                starFx.Add(new SeaStarPRT(p));
            }
            for (int i = starFx.Count - 1; i >= 0; i--) {
                if (starFx[i].Update(panelPos, panelSize)) {
                    starFx.RemoveAt(i);
                }
            }

            float scaleW = Main.UIScale;
            bubbleSpawnTimer++;
            if (Active && bubbleSpawnTimer >= 12 && bubbles.Count < 25) {
                bubbleSpawnTimer = 0;
                float left = panelPos.X + BubbleSideMargin * scaleW;
                float right = panelPos.X + panelSize.X - BubbleSideMargin * scaleW;
                Vector2 start = new(Main.rand.NextFloat(left, right), panelPos.Y + panelSize.Y - 10f);
                var bb = new BubblePRT(start);
                bb.CoreColor = Color.LightYellow;
                bb.RimColor = Color.LimeGreen;
                bubbles.Add(bb);
            }
            for (int i = bubbles.Count - 1; i >= 0; i--) {
                if (bubbles[i].Update(panelPos, panelSize, BubbleSideMargin)) {
                    bubbles.RemoveAt(i);
                }
            }

            ashSpawnTimer++;
            if (Active && ashSpawnTimer >= 18 && ashParticles.Count < 15) {
                ashSpawnTimer = 0;
                float left = panelPos.X + BubbleSideMargin * scaleW;
                float right = panelPos.X + panelSize.X - BubbleSideMargin * scaleW;
                Vector2 start = new(Main.rand.NextFloat(left, right), panelPos.Y + panelSize.Y - 10f);
                ashParticles.Add(new AshPRT(start));
            }
            for (int i = ashParticles.Count - 1; i >= 0; i--) {
                if (ashParticles[i].Update(panelPos, panelSize)) {
                    ashParticles.RemoveAt(i);
                }
            }
        }

        protected override void DrawStyle(SpriteBatch spriteBatch, Rectangle panelRect, float alpha, float contentAlpha, float easedProgress) {
            Texture2D vaule = VaultAsset.placeholder2.Value;
            Rectangle shadow = panelRect;
            shadow.Offset(6, 8);
            spriteBatch.Draw(vaule, shadow, new Rectangle(0, 0, 1, 1), Color.Black * (alpha * 0.60f));

            int segs = 30;
            for (int i = 0; i < segs; i++) {
                float t = i / (float)segs;
                float t2 = (i + 1) / (float)segs;
                int y1 = panelRect.Y + (int)(t * panelRect.Height);
                int y2 = panelRect.Y + (int)(t2 * panelRect.Height);
                Rectangle r = new(panelRect.X, y1, panelRect.Width, Math.Max(1, y2 - y1));

                Color sulfurDeep = new Color(12, 18, 8);
                Color toxicMid = new Color(28, 38, 15);
                Color acidEdge = new Color(65, 85, 30);
                float breathing = (float)Math.Sin(sulfurPulse) * 0.5f + 0.5f;
                Color blendBase = Color.Lerp(sulfurDeep, toxicMid, (float)Math.Sin(panelPulseTimer * 0.5f + t * 1.4f) * 0.5f + 0.5f);
                Color c = Color.Lerp(blendBase, acidEdge, t * 0.7f * (0.3f + breathing * 0.7f));
                c *= alpha * 0.92f;
                spriteBatch.Draw(vaule, r, new Rectangle(0, 0, 1, 1), c);
            }

            float miasmaEffect = (float)Math.Sin(miasmaTimer * 1.1f) * 0.5f + 0.5f;
            Color miasmaTint = new Color(45, 55, 20) * (alpha * 0.4f * miasmaEffect);
            spriteBatch.Draw(vaule, panelRect, new Rectangle(0, 0, 1, 1), miasmaTint);

            DrawToxicWaveOverlay(spriteBatch, panelRect, alpha * 0.85f);

            float pulse = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 2.2f) * 0.5f + 0.5f;
            Rectangle inner = panelRect;
            inner.Inflate(-6, -6);
            spriteBatch.Draw(vaule, inner, new Rectangle(0, 0, 1, 1), new Color(80, 100, 35) * (alpha * 0.09f * (0.5f + pulse * 0.5f)));

            DrawFrameSulfur(spriteBatch, panelRect, alpha, pulse);

            foreach (var ash in ashParticles) {
                ash.Draw(spriteBatch, alpha * 0.75f);
            }
            foreach (var b in bubbles) {
                b.DrawEnhanced(spriteBatch, alpha * 0.9f);
            }
            foreach (var s in starFx) {
                s.DrawEnhanced(spriteBatch, alpha * 0.4f);
            }

            if (current == null || contentAlpha <= 0.01f) {
                return;
            }

            DrawPortraitAndText(spriteBatch, panelRect, alpha, contentAlpha);
        }

        #region 样式工具函数
        private void DrawToxicWaveOverlay(SpriteBatch sb, Rectangle rect, float alpha) {
            Texture2D vaule = VaultAsset.placeholder2.Value;
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
                            sb.Draw(vaule, prev, new Rectangle(0, 0, 1, 1), c, rot, Vector2.Zero, new Vector2(len, thickness), SpriteEffects.None, 0f);
                        }
                    }
                    prev = point;
                }
            }
        }

        private static void DrawFrameSulfur(SpriteBatch sb, Rectangle rect, float alpha, float pulse) {
            Texture2D vaule = VaultAsset.placeholder2.Value;
            Color edge = Color.Lerp(new Color(70, 100, 35), new Color(130, 160, 65), pulse) * (alpha * 0.85f);
            sb.Draw(vaule, new Rectangle(rect.X, rect.Y, rect.Width, 2), new Rectangle(0, 0, 1, 1), edge);
            sb.Draw(vaule, new Rectangle(rect.X, rect.Bottom - 2, rect.Width, 2), new Rectangle(0, 0, 1, 1), edge * 0.75f);
            sb.Draw(vaule, new Rectangle(rect.X, rect.Y, 2, rect.Height), new Rectangle(0, 0, 1, 1), edge * 0.88f);
            sb.Draw(vaule, new Rectangle(rect.Right - 2, rect.Y, 2, rect.Height), new Rectangle(0, 0, 1, 1), edge * 0.88f);

            Rectangle inner = rect;
            inner.Inflate(-5, -5);
            Color innerC = new Color(140, 170, 70) * (alpha * 0.22f * pulse);
            sb.Draw(vaule, new Rectangle(inner.X, inner.Y, inner.Width, 1), new Rectangle(0, 0, 1, 1), innerC);
            sb.Draw(vaule, new Rectangle(inner.X, inner.Bottom - 1, inner.Width, 1), new Rectangle(0, 0, 1, 1), innerC * 0.7f);
            sb.Draw(vaule, new Rectangle(inner.X, inner.Y, 1, inner.Height), new Rectangle(0, 0, 1, 1), innerC * 0.88f);
            sb.Draw(vaule, new Rectangle(inner.Right - 1, inner.Y, 1, inner.Height), new Rectangle(0, 0, 1, 1), innerC * 0.88f);

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

        private static void DrawSulfseaGlowRect(SpriteBatch sb, Rectangle rect, Color glow) {
            Texture2D vaule = VaultAsset.placeholder2.Value;
            sb.Draw(vaule, rect, new Rectangle(0, 0, 1, 1), glow * 0.18f);
            int border = 2;
            sb.Draw(vaule, new Rectangle(rect.X, rect.Y, rect.Width, border), new Rectangle(0, 0, 1, 1), glow * 0.65f);
            sb.Draw(vaule, new Rectangle(rect.X, rect.Bottom - border, rect.Width, border), new Rectangle(0, 0, 1, 1), glow * 0.45f);
            sb.Draw(vaule, new Rectangle(rect.X, rect.Y, border, rect.Height), new Rectangle(0, 0, 1, 1), glow * 0.55f);
            sb.Draw(vaule, new Rectangle(rect.Right - border, rect.Y, border, rect.Height), new Rectangle(0, 0, 1, 1), glow * 0.55f);
        }
        #endregion
    }
}

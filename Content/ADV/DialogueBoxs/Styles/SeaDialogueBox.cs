using CalamityOverhaul.Content.ADV.UIEffect;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.ADV.DialogueBoxs.Styles
{
    /// <summary>
    /// 特化的深海风格 GalGame 对话框
    /// </summary>
    internal class SeaDialogueBox : DialogueBoxBase
    {
        public static SeaDialogueBox Instance => UIHandleLoader.GetUIHandleOfType<SeaDialogueBox>();
        public override string LocalizationCategory => "UI";

        //风格参数
        private const float FixedWidth = 520f;
        protected override float PanelWidth => FixedWidth;

        //背景动画参数
        private float panelPulseTimer = 0f;
        private float scanTimer = 0f;
        private float wavePhase = 0f;
        private float abyssPulse = 0f;

        //视觉粒子
        private readonly List<SeaStarPRT> starFx = [];
        private int starSpawnTimer = 0;
        private readonly List<BubblePRT> bubbles = [];
        private int bubbleSpawnTimer = 0;
        private const float BubbleSideMargin = 34f;

        #region 样式配置重写

        protected override Color GetSilhouetteColor(ContentDrawContext ctx) => new Color(10, 30, 40) * 0.9f;

        protected override Color GetTextLineColor(ContentDrawContext ctx, int lineIndex) {
            return Color.Lerp(new Color(180, 230, 250), Color.White, 0.35f) * ctx.ContentAlpha;
        }

        protected override Vector2 ApplyTextLineOffset(ContentDrawContext ctx, Vector2 basePosition, int lineIndex) {
            float wobble = (float)Math.Sin(wavePhase * 2.2f + lineIndex * 0.55f) * 1.2f;
            return basePosition + new Vector2(wobble, 0);
        }

        protected override Color GetContinueHintColor(ContentDrawContext ctx, float blink) {
            return new Color(140, 230, 255) * blink * ctx.ContentAlpha;
        }

        protected override Color GetFastHintColor(ContentDrawContext ctx) {
            return new Color(120, 200, 235) * 0.4f * ctx.ContentAlpha;
        }

        #endregion

        #region 抽象方法实现

        protected override void DrawPortraitFrame(ContentDrawContext ctx, Rectangle frameRect) {
            Texture2D vaule = VaultAsset.placeholder2.Value;
            float alpha = ctx.Alpha * ctx.PortraitData.Fade * ctx.PortraitExtraAlpha;

            Color back = new Color(5, 20, 28) * (alpha * 0.85f);
            ctx.SpriteBatch.Draw(vaule, frameRect, new Rectangle(0, 0, 1, 1), back);

            Color edge = new Color(70, 180, 230) * (alpha * 0.6f);
            ctx.SpriteBatch.Draw(vaule, new Rectangle(frameRect.X, frameRect.Y, frameRect.Width, 2), new Rectangle(0, 0, 1, 1), edge);
            ctx.SpriteBatch.Draw(vaule, new Rectangle(frameRect.X, frameRect.Bottom - 2, frameRect.Width, 2), new Rectangle(0, 0, 1, 1), edge * 0.7f);
            ctx.SpriteBatch.Draw(vaule, new Rectangle(frameRect.X, frameRect.Y, 2, frameRect.Height), new Rectangle(0, 0, 1, 1), edge * 0.8f);
            ctx.SpriteBatch.Draw(vaule, new Rectangle(frameRect.Right - 2, frameRect.Y, 2, frameRect.Height), new Rectangle(0, 0, 1, 1), edge * 0.8f);
        }

        protected override void DrawPortraitGlow(ContentDrawContext ctx, Rectangle glowRect) {
            var pd = ctx.PortraitData;
            Color rim = new Color(140, 230, 255) * (ctx.ContentAlpha * 0.4f * (float)Math.Sin(panelPulseTimer * 1.2f + pd.Fade) * pd.Fade + 0.4f * pd.Fade) * ctx.PortraitExtraAlpha;
            DrawGlowRect(ctx.SpriteBatch, glowRect, rim);
        }

        protected override void DrawNameGlow(ContentDrawContext ctx, Vector2 position, float alpha) {
            Color nameGlow = new Color(140, 230, 255) * alpha * 0.7f;
            for (int i = 0; i < NameGlowCount; i++) {
                float a = MathHelper.TwoPi * i / NameGlowCount;
                Vector2 off = a.ToRotationVector2() * NameGlowRadius * ctx.SwitchEase;
                Utils.DrawBorderString(ctx.SpriteBatch, current.Speaker, position + off, nameGlow * 0.55f, NameScale);
            }
        }

        protected override void DrawDividerLine(ContentDrawContext ctx, Vector2 start, Vector2 end, float alpha) {
            DrawGradientLine(ctx.SpriteBatch, start, end,
                new Color(70, 180, 230) * (alpha * 0.85f),
                new Color(70, 180, 230) * (alpha * 0.05f),
                DividerLineThickness);
        }

        #endregion

        protected override void StyleUpdate(Vector2 panelPos, Vector2 panelSize) {
            panelPulseTimer += 0.035f;
            scanTimer += 0.022f;
            wavePhase += 0.018f;
            abyssPulse += 0.01f;
            if (panelPulseTimer > MathHelper.TwoPi) panelPulseTimer -= MathHelper.TwoPi;
            if (scanTimer > MathHelper.TwoPi) scanTimer -= MathHelper.TwoPi;
            if (wavePhase > MathHelper.TwoPi) wavePhase -= MathHelper.TwoPi;
            if (abyssPulse > MathHelper.TwoPi) abyssPulse -= MathHelper.TwoPi;

            starSpawnTimer++;
            if (Active && starSpawnTimer >= 30 && starFx.Count < 10) {
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
            if (Active && bubbleSpawnTimer >= 16 && bubbles.Count < 20) {
                bubbleSpawnTimer = 0;
                float left = panelPos.X + BubbleSideMargin * scaleW;
                float right = panelPos.X + panelSize.X - BubbleSideMargin * scaleW;
                Vector2 start = new(Main.rand.NextFloat(left, right), panelPos.Y + panelSize.Y - 10f);
                bubbles.Add(new BubblePRT(start));
            }
            for (int i = bubbles.Count - 1; i >= 0; i--) {
                if (bubbles[i].Update(panelPos, panelSize, BubbleSideMargin)) {
                    bubbles.RemoveAt(i);
                }
            }
        }

        protected override void DrawStyle(SpriteBatch spriteBatch, Rectangle panelRect, float alpha, float contentAlpha, float easedProgress) {
            Texture2D vaule = VaultAsset.placeholder2.Value;
            Rectangle shadow = panelRect; shadow.Offset(6, 8);
            spriteBatch.Draw(vaule, shadow, new Rectangle(0, 0, 1, 1), Color.Black * (alpha * 0.50f));

            int segs = 30;
            for (int i = 0; i < segs; i++) {
                float t = i / (float)segs;
                float t2 = (i + 1) / (float)segs;
                int y1 = panelRect.Y + (int)(t * panelRect.Height);
                int y2 = panelRect.Y + (int)(t2 * panelRect.Height);
                Rectangle r = new(panelRect.X, y1, panelRect.Width, Math.Max(1, y2 - y1));
                Color abyssDeep = new Color(2, 10, 18);
                Color abyssMid = new Color(6, 32, 48);
                Color bioEdge = new Color(12, 80, 110);
                float breathing = (float)Math.Sin(abyssPulse) * 0.5f + 0.5f;
                Color blendBase = Color.Lerp(abyssDeep, abyssMid, (float)Math.Sin(panelPulseTimer * 0.4f + t * 1.6f) * 0.5f + 0.5f);
                Color c = Color.Lerp(blendBase, bioEdge, t * 0.6f * (0.4f + breathing * 0.6f));
                c *= alpha * 0.95f;
                spriteBatch.Draw(vaule, r, new Rectangle(0, 0, 1, 1), c);
            }

            float darkPulse = (float)Math.Sin(abyssPulse * 1.3f) * 0.5f + 0.5f;
            Color vignette = new Color(0, 20, 28) * (alpha * 0.35f * darkPulse);
            spriteBatch.Draw(vaule, panelRect, new Rectangle(0, 0, 1, 1), vignette);
            DrawWaveOverlay(spriteBatch, panelRect, alpha * 0.9f);

            float pulse = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 1.8f) * 0.5f + 0.5f;
            Rectangle inner = panelRect; inner.Inflate(-6, -6);
            spriteBatch.Draw(vaule, inner, new Rectangle(0, 0, 1, 1), new Color(30, 120, 150) * (alpha * 0.07f * (0.4f + pulse * 0.6f)));
            DrawFrameOcean(spriteBatch, panelRect, alpha, pulse);

            foreach (var b in bubbles) {
                b.DrawEnhanced(spriteBatch, alpha * 0.9f);
            }
            foreach (var s in starFx) {
                s.DrawEnhanced(spriteBatch, alpha * 0.45f);
            }

            //绘制定时对话进度指示器(在对话框之后绘制，作为叠加层)
            DrawTimedProgressIndicator(spriteBatch, panelRect, alpha);

            if (current == null || contentAlpha <= 0.01f) {
                return;
            }

            DrawPortraitAndText(spriteBatch, panelRect, alpha, contentAlpha);
        }

        #region 样式工具函数
        private void DrawWaveOverlay(SpriteBatch sb, Rectangle rect, float alpha) {
            Texture2D vaule = VaultAsset.placeholder2.Value;
            int bands = 6;
            for (int i = 0; i < bands; i++) {
                float t = i / (float)bands;
                float y = rect.Y + 18 + t * (rect.Height - 36);
                float amp = 6f + (float)Math.Sin((wavePhase + t) * 2f) * 4f;
                float thickness = 2f;
                int segments = 42;
                Vector2 prev = Vector2.Zero;
                for (int s = 0; s <= segments; s++) {
                    float p = s / (float)segments;
                    float localY = y + (float)Math.Sin(wavePhase * 2f + p * MathHelper.TwoPi * 1.2f + t) * amp;
                    Vector2 point = new(rect.X + 8 + p * (rect.Width - 16), localY);
                    if (s > 0) {
                        Vector2 diff = point - prev;
                        float len = diff.Length();
                        if (len > 0.01f) {
                            float rot = diff.ToRotation();
                            Color c = new Color(30, 120, 170) * (alpha * 0.07f);
                            sb.Draw(vaule, prev, new Rectangle(0, 0, 1, 1), c, rot, Vector2.Zero, new Vector2(len, thickness), SpriteEffects.None, 0f);
                        }
                    }
                    prev = point;
                }
            }
        }

        private static void DrawFrameOcean(SpriteBatch sb, Rectangle rect, float alpha, float pulse) {
            Texture2D vaule = VaultAsset.placeholder2.Value;
            Color edge = Color.Lerp(new Color(30, 140, 190), new Color(90, 210, 255), pulse) * (alpha * 0.8f);
            sb.Draw(vaule, new Rectangle(rect.X, rect.Y, rect.Width, 2), new Rectangle(0, 0, 1, 1), edge);
            sb.Draw(vaule, new Rectangle(rect.X, rect.Bottom - 2, rect.Width, 2), new Rectangle(0, 0, 1, 1), edge * 0.7f);
            sb.Draw(vaule, new Rectangle(rect.X, rect.Y, 2, rect.Height), new Rectangle(0, 0, 1, 1), edge * 0.85f);
            sb.Draw(vaule, new Rectangle(rect.Right - 2, rect.Y, 2, rect.Height), new Rectangle(0, 0, 1, 1), edge * 0.85f);
            Rectangle inner = rect; inner.Inflate(-5, -5);
            Color innerC = new Color(120, 220, 255) * (alpha * 0.18f * pulse);
            sb.Draw(vaule, new Rectangle(inner.X, inner.Y, inner.Width, 1), new Rectangle(0, 0, 1, 1), innerC);
            sb.Draw(vaule, new Rectangle(inner.X, inner.Bottom - 1, inner.Width, 1), new Rectangle(0, 0, 1, 1), innerC * 0.65f);
            sb.Draw(vaule, new Rectangle(inner.X, inner.Y, 1, inner.Height), new Rectangle(0, 0, 1, 1), innerC * 0.85f);
            sb.Draw(vaule, new Rectangle(inner.Right - 1, inner.Y, 1, inner.Height), new Rectangle(0, 0, 1, 1), innerC * 0.85f);
            DrawCornerStar(sb, new Vector2(rect.X + 10, rect.Y + 10), alpha * 0.9f);
            DrawCornerStar(sb, new Vector2(rect.Right - 10, rect.Y + 10), alpha * 0.9f);
            DrawCornerStar(sb, new Vector2(rect.X + 10, rect.Bottom - 10), alpha * 0.6f);
            DrawCornerStar(sb, new Vector2(rect.Right - 10, rect.Bottom - 10), alpha * 0.6f);
        }

        private static void DrawCornerStar(SpriteBatch sb, Vector2 pos, float a) {
            Texture2D px = VaultAsset.placeholder2.Value;
            float size = 5f;
            Color c = new Color(150, 230, 255) * a;
            sb.Draw(px, pos, new Rectangle(0, 0, 1, 1), c, 0f, new Vector2(0.5f, 0.5f), new Vector2(size, size * 0.26f), SpriteEffects.None, 0f);
            sb.Draw(px, pos, new Rectangle(0, 0, 1, 1), c * 0.8f, MathHelper.PiOver2, new Vector2(0.5f, 0.5f), new Vector2(size, size * 0.26f), SpriteEffects.None, 0f);
        }
        #endregion
    }
}
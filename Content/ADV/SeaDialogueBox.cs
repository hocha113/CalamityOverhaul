using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;

namespace CalamityOverhaul.Content.ADV
{
    /// <summary>
    /// 特化的深海风格 GalGame 对话框
    /// </summary>
    internal class SeaDialogueBox : DialogueBoxBase
    {
        public static SeaDialogueBox Instance => UIHandleLoader.GetUIHandleOfType<SeaDialogueBox>();
        public override string LocalizationCategory => "Legend.HalibutText";

        //风格参数
        private const float FixedWidth = 520f; //固定宽度
        protected override float PanelWidth => FixedWidth;

        //背景动画参数
        private float panelPulseTimer = 0f;
        private float scanTimer = 0f;
        private float wavePhase = 0f; //水波相位
        private float abyssPulse = 0f; //深渊呼吸相位

        //视觉粒子
        private readonly List<StarFx> starFx = [];
        private int starSpawnTimer = 0;
        private readonly List<BubbleFx> bubbles = [];
        private int bubbleSpawnTimer = 0;
        private const float BubbleSideMargin = 34f; //泡泡水平边距控制

        protected override void StyleUpdate(Vector2 panelPos, Vector2 panelSize) {
            //背景动画计时器
            panelPulseTimer += 0.035f;
            scanTimer += 0.022f;
            wavePhase += 0.018f;
            abyssPulse += 0.01f;
            if (panelPulseTimer > MathHelper.TwoPi) panelPulseTimer -= MathHelper.TwoPi;
            if (scanTimer > MathHelper.TwoPi) scanTimer -= MathHelper.TwoPi;
            if (wavePhase > MathHelper.TwoPi) wavePhase -= MathHelper.TwoPi;
            if (abyssPulse > MathHelper.TwoPi) abyssPulse -= MathHelper.TwoPi;

            //星粒子刷新
            starSpawnTimer++;
            if (Active && starSpawnTimer >= 30 && starFx.Count < 10) {
                starSpawnTimer = 0;
                Vector2 p = panelPos + new Vector2(Main.rand.NextFloat(BubbleSideMargin, panelSize.X - BubbleSideMargin), Main.rand.NextFloat(56f, panelSize.Y - 56f));
                starFx.Add(new StarFx(p));
            }
            for (int i = starFx.Count - 1; i >= 0; i--) {
                if (starFx[i].Update(panelPos, panelSize)) {
                    starFx.RemoveAt(i);
                }
            }

            //气泡刷新
            float scaleW = Main.UIScale;
            bubbleSpawnTimer++;
            if (Active && bubbleSpawnTimer >= 16 && bubbles.Count < 20) {
                bubbleSpawnTimer = 0;
                float left = panelPos.X + BubbleSideMargin * scaleW;
                float right = panelPos.X + panelSize.X - BubbleSideMargin * scaleW;
                Vector2 start = new(Main.rand.NextFloat(left, right), panelPos.Y + panelSize.Y - 10f);
                bubbles.Add(new BubbleFx(start));
            }
            for (int i = bubbles.Count - 1; i >= 0; i--) {
                if (bubbles[i].Update(panelPos, panelSize)) {
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
                b.Draw(spriteBatch, alpha * 0.9f);
            }
            foreach (var s in starFx) {
                s.Draw(spriteBatch, alpha * 0.45f);
            }
            if (current == null || contentAlpha <= 0.01f) {
                return;
            }
            DrawPortraitAndText(spriteBatch, panelRect, alpha, contentAlpha);
        }

        private void DrawPortraitAndText(SpriteBatch spriteBatch, Rectangle panelRect, float alpha, float contentAlpha) {
            DynamicSpriteFont font = FontAssets.MouseText.Value;
            bool hasPortrait = false;
            PortraitData speakerPortrait = null;
            if (current != null && !string.IsNullOrEmpty(current.Speaker) && portraits.TryGetValue(current.Speaker, out var pd) && pd.Texture != null && pd.Fade > 0.02f) {
                hasPortrait = true;
                speakerPortrait = pd;
            }
            float switchEase = speakerSwitchProgress;
            if (switchEase < 1f) {
                switchEase = CWRUtils.EaseOutCubic(switchEase);
            }
            float portraitAppearScale = MathHelper.Lerp(0.85f, 1f, switchEase);
            float portraitExtraAlpha = MathHelper.Clamp(switchEase, 0f, 1f);

            float leftOffset = Padding;
            float topNameOffset = 10f;
            float textBlockOffsetY = Padding + 36;
            if (hasPortrait) {
                float availHeight = panelRect.Height - 54f;
                float maxPortraitHeight = Math.Clamp(availHeight, 90f, 260f);
                Texture2D ptex = speakerPortrait.Texture;
                float scaleBase = Math.Min(PortraitWidth / ptex.Width, maxPortraitHeight / ptex.Height);
                float scale = scaleBase * portraitAppearScale;
                Vector2 pSize = ptex.Size() * scale;
                Vector2 pPos = new(panelRect.X + Padding + PortraitInnerPadding, panelRect.Y + panelRect.Height - pSize.Y - Padding - 10f);
                DrawPortraitFrame(spriteBatch, new Rectangle((int)(pPos.X - 8), (int)(pPos.Y - 8), (int)(pSize.X + 16), (int)(pSize.Y + 16)), alpha * speakerPortrait.Fade * portraitExtraAlpha);
                Color drawColor = speakerPortrait.BaseColor * contentAlpha * speakerPortrait.Fade * portraitExtraAlpha;
                if (speakerPortrait.Silhouette) {
                    drawColor = new Color(10, 30, 40) * (contentAlpha * speakerPortrait.Fade * portraitExtraAlpha) * 0.9f;
                }
                spriteBatch.Draw(ptex, pPos, null, drawColor, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                Color rim = new Color(140, 230, 255) * (contentAlpha * 0.4f * (float)Math.Sin(panelPulseTimer * 1.2f + speakerPortrait.Fade) * speakerPortrait.Fade + 0.4f * speakerPortrait.Fade) * portraitExtraAlpha;
                DrawGlowRect(spriteBatch, new Rectangle((int)pPos.X - 4, (int)pPos.Y - 4, (int)pSize.X + 8, (int)pSize.Y + 8), rim);
                leftOffset += PortraitWidth + 20f;
            }
            if (current != null && !string.IsNullOrEmpty(current.Speaker)) {
                Vector2 speakerPos = new(panelRect.X + leftOffset, panelRect.Y + topNameOffset - (1f - switchEase) * 6f);
                float nameAlpha = contentAlpha * switchEase;
                Color nameGlow = new Color(140, 230, 255) * nameAlpha * 0.7f;
                for (int i = 0; i < 4; i++) {
                    float a = MathHelper.TwoPi * i / 4f;
                    Vector2 off = a.ToRotationVector2() * 1.8f * switchEase;
                    Utils.DrawBorderString(spriteBatch, current.Speaker, speakerPos + off, nameGlow * 0.55f, 0.9f);
                }
                Utils.DrawBorderString(spriteBatch, current.Speaker, speakerPos, Color.White * nameAlpha, 0.9f);
                Vector2 divStart = speakerPos + new Vector2(0, 26);
                Vector2 divEnd = divStart + new Vector2(panelRect.Width - leftOffset - Padding, 0);
                float lineAlpha = contentAlpha * switchEase;
                DrawGradientLine(spriteBatch, divStart, divEnd, new Color(70, 180, 230) * (lineAlpha * 0.85f), new Color(70, 180, 230) * (lineAlpha * 0.05f), 1.3f);
            }
            Vector2 textStart = new(panelRect.X + leftOffset, panelRect.Y + textBlockOffsetY);
            int remaining = visibleCharCount;
            int lineHeight = (int)(font.MeasureString("A").Y * 0.8f) + LineSpacing;
            int maxLines = (int)((panelRect.Height - (textStart.Y - panelRect.Y) - Padding) / lineHeight);
            for (int i = 0; i < wrappedLines.Length && i < maxLines; i++) {
                string fullLine = wrappedLines[i];
                if (string.IsNullOrEmpty(fullLine)) {
                    continue;
                }
                string visLine;
                if (finishedCurrent) {
                    visLine = fullLine;
                }
                else {
                    if (remaining <= 0) {
                        break;
                    }
                    int take = Math.Min(fullLine.Length, remaining);
                    visLine = fullLine[..take];
                    remaining -= take;
                }
                Vector2 linePos = textStart + new Vector2(0, i * lineHeight);
                if (linePos.Y + lineHeight > panelRect.Bottom - Padding) {
                    break;
                }
                float wobble = (float)Math.Sin(wavePhase * 2.2f + i * 0.55f) * 1.2f;
                Vector2 wobblePos = linePos + new Vector2(wobble, 0);
                Color lineColor = Color.Lerp(new Color(180, 230, 250), Color.White, 0.35f) * contentAlpha;
                Utils.DrawBorderString(spriteBatch, visLine, wobblePos, lineColor, 0.8f);
            }
            if (waitingForAdvance) {
                float blink = (float)Math.Sin(advanceBlinkTimer / 12f * MathHelper.TwoPi) * 0.5f + 0.5f;
                string hint = $"> {ContinueHint.Value}<";
                Vector2 hintSize = font.MeasureString(hint) * 0.6f;
                Vector2 hintPos = new(panelRect.Right - Padding - hintSize.X, panelRect.Bottom - Padding - hintSize.Y);
                Utils.DrawBorderString(spriteBatch, hint, hintPos, new Color(140, 230, 255) * blink * contentAlpha, 0.8f);
            }
            if (!finishedCurrent) {
                string fast = FastHint.Value;
                Vector2 fastSize = font.MeasureString(fast) * 0.6f;
                Vector2 fastPos = new(panelRect.Right - Padding - fastSize.X, panelRect.Bottom - Padding - fastSize.Y - 16);
                Utils.DrawBorderString(spriteBatch, fast, fastPos, new Color(120, 200, 235) * 0.4f * contentAlpha, 0.7f);
            }
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

        private static void DrawGradientLine(SpriteBatch spriteBatch, Vector2 start, Vector2 end, Color startColor, Color endColor, float thickness) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Vector2 edge = end - start;
            float length = edge.Length();
            if (length < 1f) {
                return;
            }
            edge.Normalize();
            float rotation = (float)Math.Atan2(edge.Y, edge.X);
            int segments = Math.Max(1, (int)(length / 11f));
            for (int i = 0; i < segments; i++) {
                float t = i / (float)segments;
                Vector2 segPos = start + edge * (length * t);
                float segLength = length / segments;
                Color color = Color.Lerp(startColor, endColor, t);
                spriteBatch.Draw(pixel, segPos, new Rectangle(0, 0, 1, 1), color, rotation, new Vector2(0, 0.5f), new Vector2(segLength, thickness), SpriteEffects.None, 0);
            }
        }
        #endregion

        #region 粒子内部类
        private class StarFx(Vector2 p)
        {
            public Vector2 Pos = p;
            public float BaseRadius = Main.rand.NextFloat(2f, 4f);
            public float Rot = Main.rand.NextFloat(MathHelper.TwoPi);
            public float Life = 0f;
            public float MaxLife = Main.rand.NextFloat(60f, 140f);
            public float Seed = Main.rand.NextFloat(10f);

            public bool Update(Vector2 panelPos, Vector2 panelSize) {
                Life++; Rot += 0.02f; float t = Life / MaxLife;
                float drift = (float)Math.Sin((Life + Seed * 20f) * 0.03f) * 6f; Pos.X += drift * 0.02f; if (Life >= MaxLife) return true;
                if (Pos.X < panelPos.X - 40 || Pos.X > panelPos.X + panelSize.X + 40 || Pos.Y < panelPos.Y - 40 || Pos.Y > panelPos.Y + panelSize.Y + 40)
                    return true;
                return false;
            }
            public void Draw(SpriteBatch sb, float alpha) {
                float t = Life / MaxLife;
                float fade = (float)Math.Sin(t * MathHelper.Pi) * alpha;
                float scale = BaseRadius * (0.6f + (float)Math.Sin((Life + Seed * 33f) * 0.08f) * 0.4f);
                Color c = Color.Gold * (0.7f * fade);
                Texture2D px = VaultAsset.placeholder2.Value;
                sb.Draw(px, Pos, new Rectangle(0, 0, 1, 1), c, 0f, new Vector2(0.5f, 0.5f), new Vector2(scale, scale * 0.25f), SpriteEffects.None, 0f);
                sb.Draw(px, Pos, new Rectangle(0, 0, 1, 1), c * 0.8f, MathHelper.PiOver2, new Vector2(0.5f, 0.5f), new Vector2(scale, scale * 0.25f), SpriteEffects.None, 0f);
            }
        }
        private class BubbleFx
        {
            public Vector2 Pos;
            public float Radius;
            public float RiseSpeed;
            public float Drift;
            public float Life;
            public float MaxLife;
            public float Seed;
            public BubbleFx(Vector2 start) {
                Pos = start;
                Radius = Main.rand.NextFloat(3f, 7f);
                RiseSpeed = Main.rand.NextFloat(0.55f, 1.25f);
                Drift = Main.rand.NextFloat(-0.18f, 0.18f);
                Life = 0f;
                MaxLife = Main.rand.NextFloat(90f, 160f);
                Seed = Main.rand.NextFloat(10f);
            }
            public bool Update(Vector2 panelPos, Vector2 panelSize) {
                Life++;
                float t = Life / MaxLife;
                Pos.Y -= RiseSpeed * (0.85f + (float)Math.Sin(t * Math.PI) * 0.25f);
                Pos.X += (float)Math.Sin(Life * 0.045f + Seed) * Drift;
                float left = panelPos.X + BubbleSideMargin * 0.7f;
                float right = panelPos.X + panelSize.X - BubbleSideMargin * 0.7f;
                if (Pos.X < left) Pos.X = left; if (Pos.X > right)
                    Pos.X = right;
                if (Life >= MaxLife || Pos.Y < panelPos.Y + 24f)
                    return true;
                return false;
            }
            public void Draw(SpriteBatch sb, float alpha) {
                Texture2D px = VaultAsset.placeholder2.Value;
                float t = Life / MaxLife;
                float fade = (float)Math.Sin(t * Math.PI);
                float scale = Radius * (0.9f + (float)Math.Sin((Life + Seed * 15f) * 0.1f) * 0.18f);
                Color core = new Color(140, 230, 255) * (alpha * 0.55f * fade);
                Color rim = new Color(30, 100, 150) * (alpha * 0.4f * fade);
                sb.Draw(px, Pos, new Rectangle(0, 0, 1, 1), rim, 0f, new Vector2(0.5f, 0.5f), new Vector2(scale * 1.8f, scale * 0.55f), SpriteEffects.None, 0f);
                sb.Draw(px, Pos, new Rectangle(0, 0, 1, 1), core, 0f, new Vector2(0.5f, 0.5f), new Vector2(scale, scale), SpriteEffects.None, 0f);
            }
        }
        #endregion

        #region 公共静态绘制碎片
        private static void DrawPortraitFrame(SpriteBatch sb, Rectangle rect, float alpha) {
            Texture2D vaule = VaultAsset.placeholder2.Value;
            Color back = new Color(5, 20, 28) * (alpha * 0.85f);
            sb.Draw(vaule, rect, new Rectangle(0, 0, 1, 1), back);
            Color edge = new Color(70, 180, 230) * (alpha * 0.6f);
            sb.Draw(vaule, new Rectangle(rect.X, rect.Y, rect.Width, 2), new Rectangle(0, 0, 1, 1), edge);
            sb.Draw(vaule, new Rectangle(rect.X, rect.Bottom - 2, rect.Width, 2), new Rectangle(0, 0, 1, 1), edge * 0.7f);
            sb.Draw(vaule, new Rectangle(rect.X, rect.Y, 2, rect.Height), new Rectangle(0, 0, 1, 1), edge * 0.8f);
            sb.Draw(vaule, new Rectangle(rect.Right - 2, rect.Y, 2, rect.Height), new Rectangle(0, 0, 1, 1), edge * 0.8f);
        }

        private static void DrawGlowRect(SpriteBatch sb, Rectangle rect, Color glow) {
            Texture2D vaule = VaultAsset.placeholder2.Value;
            sb.Draw(vaule, rect, new Rectangle(0, 0, 1, 1), glow * 0.15f);
            int border = 2;
            sb.Draw(vaule, new Rectangle(rect.X, rect.Y, rect.Width, border), new Rectangle(0, 0, 1, 1), glow * 0.6f);
            sb.Draw(vaule, new Rectangle(rect.X, rect.Bottom - border, rect.Width, border), new Rectangle(0, 0, 1, 1), glow * 0.4f);
            sb.Draw(vaule, new Rectangle(rect.X, rect.Y, border, rect.Height), new Rectangle(0, 0, 1, 1), glow * 0.5f);
            sb.Draw(vaule, new Rectangle(rect.Right - border, rect.Y, border, rect.Height), new Rectangle(0, 0, 1, 1), glow * 0.5f);
        }
        #endregion
    }
}
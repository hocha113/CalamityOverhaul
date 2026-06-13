using CalamityOverhaul.Content.ADV.UIEffect;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.ADV.DialogueBoxs.Styles
{
    /// <summary>
    /// 星流风格对话框
    /// </summary>
    internal class StarStreamDialogueBox : DialogueBoxBase
    {
        public static StarStreamDialogueBox Instance => UIHandleLoader.GetUIHandleOfType<StarStreamDialogueBox>();
        public override string LocalizationCategory => "UI";

        // 风格参数
        private const float FixedWidth = 540f;
        protected override float PanelWidth => FixedWidth;

        // 动画计时
        private float starFlowTimer = 0f;
        private float nebulaPulseTimer = 0f;
        private float constellationPhase = 0f;
        private float auroraTimer = 0f;
        private float shimmerTimer = 0f;

        // 粒子
        private readonly List<StarStreamPRT> starStreams = [];
        private int starStreamSpawnTimer = 0;
        private readonly List<StarDustPRT> starDusts = [];
        private int starDustSpawnTimer = 0;
        private const float SideMargin = 30f;

        #region 样式配置

        protected override float PortraitScaleMin => 0.88f;
        protected override float TopNameOffsetBase => 12f;
        protected override float TextBlockOffsetBase => 38f;
        protected override float NameScale => 0.95f;
        protected override float TextScale => 0.82f;
        protected override int NameGlowCount => 5;
        protected override float NameGlowRadius => 2.2f;
        protected override float PortraitAvailHeightOffset => 50f;
        protected override float PortraitMinHeight => 100f;
        protected override float PortraitMaxHeight => 270f;
        protected override float PortraitFramePadding => 6f;
        protected override float PortraitGlowPadding => 3f;
        protected override float PortraitLeftMargin => 22f;

        protected override Color GetSilhouetteColor(ContentDrawContext ctx) => new Color(15, 10, 30) * 0.85f;

        protected override Color GetTextLineColor(ContentDrawContext ctx, int lineIndex) {
            //暖白偏金的文字色，确保可读性
            return Color.Lerp(new Color(255, 245, 220), new Color(255, 255, 245), 0.3f) * ctx.ContentAlpha;
        }

        protected override Vector2 ApplyTextLineOffset(ContentDrawContext ctx, Vector2 basePosition, int lineIndex) {
            float drift = (float)Math.Sin(starFlowTimer * 1.5f + lineIndex * 0.5f) * 0.7f;
            return basePosition + new Vector2(drift, 0);
        }

        protected override void DrawTextLineGlow(ContentDrawContext ctx, string text, Vector2 position, int lineIndex) {
            //金色微光底层
            Color textGlow = new Color(255, 200, 80) * (ctx.ContentAlpha * 0.08f);
            Utils.DrawBorderString(ctx.SpriteBatch, text, position + new Vector2(0, 1), textGlow, TextScale);
        }

        protected override string GetContinueHintText() => $"✦ {ContinueHint.Value} ✦";

        protected override Color GetContinueHintColor(ContentDrawContext ctx, float blink) {
            return new Color(255, 210, 100) * blink * ctx.ContentAlpha;
        }

        protected override Color GetFastHintColor(ContentDrawContext ctx) {
            return new Color(200, 175, 120) * 0.45f * ctx.ContentAlpha;
        }

        protected override float ContinueHintScale => 0.82f;
        protected override float FastHintScale => 0.72f;

        //定时进度条颜色覆盖为金色系
        protected override Color TimedProgressBaseColor => new Color(255, 210, 100);
        protected override Color TimedProgressWarningColor => new Color(255, 160, 60);
        protected override Color TimedProgressDangerColor => new Color(255, 90, 60);

        #endregion

        #region 模板方法实现

        protected override void DrawPortraitFrame(ContentDrawContext ctx, Rectangle frameRect) {
            Texture2D px = VaultAsset.placeholder2.Value;
            float alpha = ctx.Alpha * ctx.PortraitData.Fade * ctx.PortraitExtraAlpha;

            //深空底色
            Color back = new Color(10, 8, 22) * (alpha * 0.9f);
            ctx.SpriteBatch.Draw(px, frameRect, new Rectangle(0, 0, 1, 1), back);

            //金色边框
            Color edge = new Color(220, 180, 80) * (alpha * 0.6f);
            ctx.SpriteBatch.Draw(px, new Rectangle(frameRect.X, frameRect.Y, frameRect.Width, 2), new Rectangle(0, 0, 1, 1), edge);
            ctx.SpriteBatch.Draw(px, new Rectangle(frameRect.X, frameRect.Bottom - 2, frameRect.Width, 2), new Rectangle(0, 0, 1, 1), edge * 0.7f);
            ctx.SpriteBatch.Draw(px, new Rectangle(frameRect.X, frameRect.Y, 2, frameRect.Height), new Rectangle(0, 0, 1, 1), edge * 0.85f);
            ctx.SpriteBatch.Draw(px, new Rectangle(frameRect.Right - 2, frameRect.Y, 2, frameRect.Height), new Rectangle(0, 0, 1, 1), edge * 0.85f);
        }

        protected override void DrawPortraitGlow(ContentDrawContext ctx, Rectangle glowRect) {
            var pd = ctx.PortraitData;
            float pulse = (float)Math.Sin(nebulaPulseTimer * 1.2f + pd.Fade) * 0.5f + 0.5f;
            Color starRim = new Color(255, 200, 100) * (ctx.ContentAlpha * 0.45f * pulse * pd.Fade) * ctx.PortraitExtraAlpha;
            DrawStarGlowRect(ctx.SpriteBatch, glowRect, starRim);
        }

        protected override void DrawNameGlow(ContentDrawContext ctx, Vector2 position, float alpha) {
            Color nameGlow = new Color(255, 210, 120) * alpha * 0.7f;
            for (int i = 0; i < NameGlowCount; i++) {
                float angle = MathHelper.TwoPi * i / NameGlowCount + shimmerTimer * 0.3f;
                Vector2 offset = angle.ToRotationVector2() * NameGlowRadius * ctx.SwitchEase;
                Utils.DrawBorderString(ctx.SpriteBatch, current.Speaker, position + offset, nameGlow * 0.5f, NameScale);
            }
        }

        protected override void DrawSpeakerName(ContentDrawContext ctx) {
            Vector2 speakerPos = GetSpeakerNamePosition(ctx);
            float nameAlpha = ctx.ContentAlpha * ctx.SwitchEase;

            DrawNameGlow(ctx, speakerPos, nameAlpha);
            //名字本体用暖白金色
            Utils.DrawBorderString(ctx.SpriteBatch, current.Speaker, speakerPos, new Color(255, 245, 220) * nameAlpha, NameScale);

            Vector2 divStart = speakerPos + new Vector2(0, 26);
            Vector2 divEnd = divStart + new Vector2(ctx.PanelRect.Width - ctx.LeftOffset - Padding, 0);
            DrawDividerLine(ctx, divStart, divEnd, nameAlpha);
        }

        protected override void DrawDividerLine(ContentDrawContext ctx, Vector2 start, Vector2 end, float alpha) {
            DrawGradientLine(ctx.SpriteBatch, start, end,
                new Color(220, 180, 80) * (alpha * 0.85f),
                new Color(220, 180, 80) * (alpha * 0.06f),
                1.5f);
        }

        #endregion

        protected override void StyleUpdate(Vector2 panelPos, Vector2 panelSize) {
            starFlowTimer += 0.04f;
            nebulaPulseTimer += 0.022f;
            constellationPhase += 0.012f;
            auroraTimer += 0.018f;
            shimmerTimer += 0.035f;

            if (starFlowTimer > MathHelper.TwoPi) starFlowTimer -= MathHelper.TwoPi;
            if (nebulaPulseTimer > MathHelper.TwoPi) nebulaPulseTimer -= MathHelper.TwoPi;
            if (constellationPhase > MathHelper.TwoPi) constellationPhase -= MathHelper.TwoPi;
            if (auroraTimer > MathHelper.TwoPi) auroraTimer -= MathHelper.TwoPi;
            if (shimmerTimer > MathHelper.TwoPi) shimmerTimer -= MathHelper.TwoPi;

            //星流粒子
            starStreamSpawnTimer++;
            if (Active && starStreamSpawnTimer >= 14 && starStreams.Count < 18) {
                starStreamSpawnTimer = 0;
                Vector2 p = panelPos + new Vector2(
                    Main.rand.NextFloat(SideMargin, panelSize.X - SideMargin),
                    Main.rand.NextFloat(30f, panelSize.Y - 20f));
                starStreams.Add(new StarStreamPRT(p));
            }
            for (int i = starStreams.Count - 1; i >= 0; i--) {
                if (starStreams[i].Update(panelPos, panelSize)) {
                    starStreams.RemoveAt(i);
                }
            }

            //星尘节点
            starDustSpawnTimer++;
            if (Active && starDustSpawnTimer >= 28 && starDusts.Count < 10) {
                starDustSpawnTimer = 0;
                float scaleW = Main.UIScale;
                float left = panelPos.X + SideMargin * scaleW;
                float right = panelPos.X + panelSize.X - SideMargin * scaleW;
                Vector2 start = new(Main.rand.NextFloat(left, right),
                    panelPos.Y + Main.rand.NextFloat(40f, panelSize.Y - 30f));
                starDusts.Add(new StarDustPRT(start));
            }
            for (int i = starDusts.Count - 1; i >= 0; i--) {
                if (starDusts[i].Update(panelPos, panelSize)) {
                    starDusts.RemoveAt(i);
                }
            }
        }

        protected override void DrawStyle(SpriteBatch spriteBatch, Rectangle panelRect, float alpha, float contentAlpha, float easedProgress) {
            Texture2D px = VaultAsset.placeholder2.Value;

            //阴影
            Rectangle shadow = panelRect;
            shadow.Offset(5, 7);
            spriteBatch.Draw(px, shadow, new Rectangle(0, 0, 1, 1), new Color(5, 0, 15) * (alpha * 0.6f));

            //深空渐变背景
            int segs = 35;
            for (int i = 0; i < segs; i++) {
                float t = i / (float)segs;
                float t2 = (i + 1) / (float)segs;
                int y1 = panelRect.Y + (int)(t * panelRect.Height);
                int y2 = panelRect.Y + (int)(t2 * panelRect.Height);
                Rectangle r = new(panelRect.X, y1, panelRect.Width, Math.Max(1, y2 - y1));

                Color deepSpace = new Color(6, 4, 16);
                Color midSpace = new Color(12, 10, 28);
                Color edgeSpace = new Color(22, 18, 45);

                float nebula = (float)Math.Sin(nebulaPulseTimer * 0.5f + t * 1.8f) * 0.5f + 0.5f;
                Color blendBase = Color.Lerp(deepSpace, midSpace, nebula);
                Color c = Color.Lerp(blendBase, edgeSpace, t * 0.5f);
                c *= alpha * 0.94f;

                spriteBatch.Draw(px, r, new Rectangle(0, 0, 1, 1), c);
            }

            //星云呼吸叠加
            float nebulaPulse = (float)Math.Sin(nebulaPulseTimer * 1.3f) * 0.5f + 0.5f;
            Color nebulaOverlay = new Color(30, 15, 50) * (alpha * 0.2f * nebulaPulse);
            spriteBatch.Draw(px, panelRect, new Rectangle(0, 0, 1, 1), nebulaOverlay);

            //极光光带
            DrawAuroraStreaks(spriteBatch, panelRect, alpha * 0.8f);
            //星座网格
            DrawConstellationGrid(spriteBatch, panelRect, alpha * 0.7f);

            //内部金色光晕
            float innerPulse = (float)Math.Sin(shimmerTimer * 1.1f) * 0.5f + 0.5f;
            Rectangle inner = panelRect;
            inner.Inflate(-5, -5);
            spriteBatch.Draw(px, inner, new Rectangle(0, 0, 1, 1), new Color(200, 160, 60) * (alpha * 0.06f * innerPulse));

            //边框
            DrawStarFrame(spriteBatch, panelRect, alpha, innerPulse);

            //粒子
            foreach (var dust in starDusts) {
                dust.Draw(spriteBatch, alpha * 0.8f);
            }
            foreach (var stream in starStreams) {
                stream.Draw(spriteBatch, alpha * 0.7f);
            }

            //定时对话进度指示器
            DrawTimedProgressIndicator(spriteBatch, panelRect, alpha);

            if (current == null || contentAlpha <= 0.01f) {
                return;
            }

            DrawPortraitAndText(spriteBatch, panelRect, alpha, contentAlpha);
        }

        #region 样式工具函数

        /// <summary>
        /// 绘制极光光带 - 在面板中横向流动的金色/蓝紫色光带
        /// </summary>
        private void DrawAuroraStreaks(SpriteBatch sb, Rectangle rect, float alpha) {
            Texture2D px = VaultAsset.placeholder2.Value;
            int streakCount = 5;

            for (int i = 0; i < streakCount; i++) {
                float t = i / (float)streakCount;
                float baseY = rect.Y + 20 + t * (rect.Height - 40);
                float amplitude = 4f + (float)Math.Sin((auroraTimer + t * 1.5f) * 2f) * 3f;
                float thickness = 1.5f;

                int segments = 40;
                Vector2 prevPoint = Vector2.Zero;
                for (int s = 0; s <= segments; s++) {
                    float progress = s / (float)segments;
                    float waveY = baseY + (float)Math.Sin(auroraTimer * 2.5f + progress * MathHelper.TwoPi * 1.2f + t * 2.5f) * amplitude;
                    Vector2 point = new(rect.X + 10 + progress * (rect.Width - 20), waveY);

                    if (s > 0) {
                        Vector2 diff = point - prevPoint;
                        float len = diff.Length();
                        if (len > 0.01f) {
                            float rot = diff.ToRotation();
                            //金色 -> 蓝紫色渐变
                            Color streakColor = Color.Lerp(
                                new Color(200, 160, 60),
                                new Color(80, 60, 160),
                                progress) * (alpha * 0.06f);
                            sb.Draw(px, prevPoint, new Rectangle(0, 0, 1, 1), streakColor, rot, Vector2.Zero, new Vector2(len, thickness), SpriteEffects.None, 0f);
                        }
                    }
                    prevPoint = point;
                }
            }
        }

        /// <summary>
        /// 绘制星座网格 - 微弱的横线随相位闪烁，模拟星图坐标
        /// </summary>
        private void DrawConstellationGrid(SpriteBatch sb, Rectangle rect, float alpha) {
            Texture2D px = VaultAsset.placeholder2.Value;
            int rows = 7;
            float rowHeight = rect.Height / (float)rows;

            for (int row = 0; row < rows; row++) {
                float t = row / (float)rows;
                float y = rect.Y + row * rowHeight;
                float phase = constellationPhase + t * MathHelper.Pi;
                float brightness = (float)Math.Sin(phase) * 0.5f + 0.5f;

                Color gridColor = new Color(180, 150, 80) * (alpha * 0.03f * brightness);
                sb.Draw(px, new Rectangle(rect.X + 12, (int)y, rect.Width - 24, 1), new Rectangle(0, 0, 1, 1), gridColor);
            }
        }

        /// <summary>
        /// 绘制星流风格边框
        /// </summary>
        private void DrawStarFrame(SpriteBatch sb, Rectangle rect, float alpha, float pulse) {
            Texture2D px = VaultAsset.placeholder2.Value;

            //外框：金色，带呼吸
            Color outerEdge = Color.Lerp(new Color(180, 140, 50), new Color(240, 200, 100), pulse) * (alpha * 0.8f);
            sb.Draw(px, new Rectangle(rect.X, rect.Y, rect.Width, 3), new Rectangle(0, 0, 1, 1), outerEdge);
            sb.Draw(px, new Rectangle(rect.X, rect.Bottom - 3, rect.Width, 3), new Rectangle(0, 0, 1, 1), outerEdge * 0.7f);
            sb.Draw(px, new Rectangle(rect.X, rect.Y, 3, rect.Height), new Rectangle(0, 0, 1, 1), outerEdge * 0.9f);
            sb.Draw(px, new Rectangle(rect.Right - 3, rect.Y, 3, rect.Height), new Rectangle(0, 0, 1, 1), outerEdge * 0.9f);

            //内框：更亮的金色光线
            Rectangle inner = rect;
            inner.Inflate(-6, -6);
            Color innerC = new Color(255, 220, 120) * (alpha * 0.18f * pulse);
            sb.Draw(px, new Rectangle(inner.X, inner.Y, inner.Width, 1), new Rectangle(0, 0, 1, 1), innerC);
            sb.Draw(px, new Rectangle(inner.X, inner.Bottom - 1, inner.Width, 1), new Rectangle(0, 0, 1, 1), innerC * 0.65f);
            sb.Draw(px, new Rectangle(inner.X, inner.Y, 1, inner.Height), new Rectangle(0, 0, 1, 1), innerC * 0.85f);
            sb.Draw(px, new Rectangle(inner.Right - 1, inner.Y, 1, inner.Height), new Rectangle(0, 0, 1, 1), innerC * 0.85f);

            //顶部金色流光高亮条 - 从左至右流动
            float flowT = (shimmerTimer * 0.8f) % 1f;
            int highlightW = 80;
            int highlightX = rect.X + (int)(flowT * (rect.Width - highlightW));
            Color highlightColor = new Color(255, 230, 140) * (alpha * 0.3f);
            for (int dx = 0; dx < highlightW; dx++) {
                float localT = dx / (float)highlightW;
                float intensity = (float)Math.Sin(localT * MathHelper.Pi);
                sb.Draw(px, new Rectangle(highlightX + dx, rect.Y, 1, 3), new Rectangle(0, 0, 1, 1), highlightColor * intensity);
            }

            //底部反向流光
            float flowB = ((shimmerTimer * 0.6f) + 0.5f) % 1f;
            int highlightBX = rect.X + (int)((1f - flowB) * (rect.Width - highlightW));
            Color highlightBColor = new Color(255, 210, 100) * (alpha * 0.2f);
            for (int dx = 0; dx < highlightW; dx++) {
                float localT = dx / (float)highlightW;
                float intensity = (float)Math.Sin(localT * MathHelper.Pi);
                sb.Draw(px, new Rectangle(highlightBX + dx, rect.Bottom - 3, 1, 3), new Rectangle(0, 0, 1, 1), highlightBColor * intensity);
            }

            //角落星辰标记
            DrawCornerStar(sb, new Vector2(rect.X + 12, rect.Y + 12), alpha * 0.95f);
            DrawCornerStar(sb, new Vector2(rect.Right - 12, rect.Y + 12), alpha * 0.95f);
            DrawCornerStar(sb, new Vector2(rect.X + 12, rect.Bottom - 12), alpha * 0.6f);
            DrawCornerStar(sb, new Vector2(rect.Right - 12, rect.Bottom - 12), alpha * 0.6f);
        }

        /// <summary>
        /// 绘制角落四芒星标记
        /// </summary>
        private static void DrawCornerStar(SpriteBatch sb, Vector2 pos, float a) {
            Texture2D px = VaultAsset.placeholder2.Value;
            float size = 6f;
            Color c = new Color(255, 220, 120) * a;

            //四芒星
            sb.Draw(px, pos, new Rectangle(0, 0, 1, 1), c, 0f, new Vector2(0.5f, 0.5f),
                new Vector2(size * 1.3f, size * 0.22f), SpriteEffects.None, 0f);
            sb.Draw(px, pos, new Rectangle(0, 0, 1, 1), c * 0.85f, MathHelper.PiOver2, new Vector2(0.5f, 0.5f),
                new Vector2(size * 1.3f, size * 0.22f), SpriteEffects.None, 0f);
            //对角线
            sb.Draw(px, pos, new Rectangle(0, 0, 1, 1), c * 0.5f, MathHelper.PiOver4, new Vector2(0.5f, 0.5f),
                new Vector2(size * 0.8f, size * 0.18f), SpriteEffects.None, 0f);
            sb.Draw(px, pos, new Rectangle(0, 0, 1, 1), c * 0.5f, -MathHelper.PiOver4, new Vector2(0.5f, 0.5f),
                new Vector2(size * 0.8f, size * 0.18f), SpriteEffects.None, 0f);
            //中心光点
            sb.Draw(px, pos, new Rectangle(0, 0, 1, 1), c * 0.7f, 0f, new Vector2(0.5f, 0.5f),
                new Vector2(size * 0.35f, size * 0.35f), SpriteEffects.None, 0f);
        }

        /// <summary>
        /// 绘制星流风格光效矩形
        /// </summary>
        private static void DrawStarGlowRect(SpriteBatch sb, Rectangle rect, Color glow) {
            Texture2D px = VaultAsset.placeholder2.Value;
            sb.Draw(px, rect, new Rectangle(0, 0, 1, 1), glow * 0.18f);

            int border = 2;
            sb.Draw(px, new Rectangle(rect.X, rect.Y, rect.Width, border), new Rectangle(0, 0, 1, 1), glow * 0.65f);
            sb.Draw(px, new Rectangle(rect.X, rect.Bottom - border, rect.Width, border), new Rectangle(0, 0, 1, 1), glow * 0.45f);
            sb.Draw(px, new Rectangle(rect.X, rect.Y, border, rect.Height), new Rectangle(0, 0, 1, 1), glow * 0.55f);
            sb.Draw(px, new Rectangle(rect.Right - border, rect.Y, border, rect.Height), new Rectangle(0, 0, 1, 1), glow * 0.55f);
        }

        #endregion
    }
}

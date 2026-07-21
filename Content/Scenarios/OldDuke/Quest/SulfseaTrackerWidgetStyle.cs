using CalamityOverhaul.Content.EntrustManager;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;

namespace CalamityOverhaul.Content.Scenarios.OldDuke.Quest
{
    /// <summary>硫磺海追踪HUD，无面板，气泡+酸绿细线</summary>
    internal class SulfseaTrackerWidgetStyle : IEntrustTrackerWidgetStyle
    {
        #region 色板

        private static readonly Color AcidGreen = new(140, 180, 70);
        private static readonly Color AcidGreenBright = new(200, 230, 120);
        private static readonly Color AcidGreenDim = new(60, 95, 25);
        private static readonly Color BubbleGlow = new(170, 210, 90);
        private static readonly Color TitleWarm = new(215, 235, 165);
        private static readonly Color TextBody = new(195, 215, 150);
        private static readonly Color ShadowInk = new(6, 12, 4);

        #endregion

        private float pulse;
        private float bubble;

        public void Update(Rectangle widgetRect, float slideProgress) {
            pulse += 0.028f;
            bubble += 0.042f;
            if (pulse > MathHelper.TwoPi) pulse -= MathHelper.TwoPi;
            if (bubble > MathHelper.TwoPi) bubble -= MathHelper.TwoPi;
        }

        public void Reset() { pulse = 0f; bubble = 0f; }

        public void DrawWidgetBackground(SpriteBatch sb, Rectangle rect, float alpha) { }
        public void DrawWidgetFrame(SpriteBatch sb, Rectangle rect, float alpha) { }

        public void DrawWidgetHeader(SpriteBatch sb, Rectangle headerRect, string title, float alpha) {
            var px = VaultAsset.placeholder2.Value;
            var uv = new Rectangle(0, 0, 1, 1);
            var font = FontAssets.MouseText.Value;

            //头标气泡簇
            DrawBubbleCluster(sb, px, uv, headerRect.X + 9, headerRect.Y + headerRect.Height / 2, alpha);

            //标题暖绿
            const float titleScale = 0.95f;
            int textX = headerRect.X + 20;
            //大字下移基线
            float textY = headerRect.Y + (headerRect.Height - 16f) / 2f;
            Vector2 titlePos = new(textX, textY);

            Utils.DrawBorderString(sb, title, titlePos + new Vector2(0, 1),
                ShadowInk * (alpha * 0.55f), titleScale);
            Utils.DrawBorderString(sb, title, titlePos, TitleWarm * alpha, titleScale);

            //下划线+气泡点
            int titlePixelW = (int)(font.MeasureString(title).X * titleScale);
            int underY = headerRect.Bottom + 1;
            int solidLen = Math.Clamp(titlePixelW + 4, 18, headerRect.Width - 40);
            float p = MathF.Sin(pulse * 1.6f) * 0.18f + 0.82f;

            int segs = Math.Max(8, solidLen / 3);
            for (int i = 0; i < segs; i++) {
                float t = i / (float)segs;
                float t2 = (i + 1) / (float)segs;
                int x1 = textX + (int)(t * solidLen);
                int x2 = textX + (int)(t2 * solidLen);
                int w = Math.Max(1, x2 - x1);
                Color c = Color.Lerp(AcidGreen, BubbleGlow, t * 0.4f) * (alpha * 0.85f * p);
                sb.Draw(px, new Rectangle(x1, underY, w, 1), uv, c);
            }

            int bubStartX = textX + solidLen + 5;
            int[] sizes = [2, 2, 1];
            for (int k = 0; k < sizes.Length; k++) {
                int bx = bubStartX + k * 6;
                int w = sizes[k];
                if (bx + w > headerRect.Right - 6) break;
                float yOff = MathF.Sin(bubble * 1.6f + k * 1.3f) * 1.4f - 0.4f;
                int by = underY + (int)yOff;
                float fade = (1f - k / (float)sizes.Length) * 0.75f;
                sb.Draw(px, new Rectangle(bx, by, w, 1), uv, AcidGreenBright * (alpha * fade));
            }
        }

        private void DrawBubbleCluster(SpriteBatch sb, Texture2D px, Rectangle uv, int cx, int cy, float alpha) {
            float p = MathF.Sin(pulse * 2f) * 0.22f + 0.78f;

            sb.Draw(px, new Vector2(cx, cy + 1), uv, BubbleGlow * (alpha * 0.18f * p),
                0f, new Vector2(0.5f), new Vector2(7f), SpriteEffects.None, 0f);
            sb.Draw(px, new Vector2(cx, cy + 1), uv, AcidGreen * (alpha * 0.88f * p),
                0f, new Vector2(0.5f), new Vector2(3f), SpriteEffects.None, 0f);
            sb.Draw(px, new Vector2(cx, cy + 1), uv, AcidGreenBright * (alpha * p),
                0f, new Vector2(0.5f), new Vector2(1.4f), SpriteEffects.None, 0f);

            //副气泡A
            float subPhaseA = bubble * 1.5f + 0.6f;
            float subYA = cy - 4f + MathF.Sin(subPhaseA) * 1.3f;
            float subFadeA = MathF.Cos(subPhaseA * 0.7f) * 0.4f + 0.6f;
            sb.Draw(px, new Rectangle(cx + 3, (int)subYA, 1, 1), uv,
                AcidGreenBright * (alpha * 0.85f * subFadeA));

            //副气泡B
            float subPhaseB = bubble * 1.9f + 2.4f;
            float subYB = cy - 7f + MathF.Sin(subPhaseB) * 1.1f;
            float subFadeB = MathF.Sin(subPhaseB * 0.6f + 0.4f) * 0.4f + 0.5f;
            sb.Draw(px, new Rectangle(cx - 3, (int)subYB, 1, 1), uv,
                AcidGreen * (alpha * 0.6f * subFadeB));
        }

        public void DrawWidgetProgress(SpriteBatch sb, Rectangle barRect, float progress,
            string progressText, float alpha) {
            var px = VaultAsset.placeholder2.Value;
            var uv = new Rectangle(0, 0, 1, 1);

            const int barH = 2;
            int y = barRect.Y + (barRect.Height - barH) / 2;
            int trackW = barRect.Width;

            //轨道底线
            sb.Draw(px, new Rectangle(barRect.X, y, trackW, barH), uv, AcidGreenDim * (alpha * 0.32f));

            //填充渐变
            int fillW = (int)(trackW * MathHelper.Clamp(progress, 0f, 1f));
            if (fillW > 0) {
                int segs = Math.Max(6, fillW / 4);
                for (int i = 0; i < segs; i++) {
                    float t = i / (float)segs;
                    float t2 = (i + 1) / (float)segs;
                    int x1 = barRect.X + (int)(t * fillW);
                    int x2 = barRect.X + (int)(t2 * fillW);
                    int w = Math.Max(1, x2 - x1);
                    Color c = Color.Lerp(AcidGreen, BubbleGlow, t * 0.65f) * (alpha * 0.92f);
                    sb.Draw(px, new Rectangle(x1, y, w, barH), uv, c);
                }
                if (fillW > 1) {
                    sb.Draw(px, new Rectangle(barRect.X + fillW - 1, y - 1, 1, barH + 2), uv,
                        AcidGreenBright * (alpha * 0.75f));
                }
                //尖端滴点
                float dripFade = MathF.Sin(bubble * 2.4f) * 0.5f + 0.5f;
                if (dripFade > 0.05f) {
                    sb.Draw(px, new Rectangle(barRect.X + fillW - 1, y + barH + 1, 1, 1), uv,
                        AcidGreenBright * (alpha * 0.55f * dripFade));
                }
            }

            if (!string.IsNullOrEmpty(progressText)) {
                var font = FontAssets.MouseText.Value;
                Vector2 sz = font.MeasureString(progressText) * 0.5f;
                Utils.DrawBorderString(sb, progressText,
                    new Vector2(barRect.Right - sz.X - 1f, y - sz.Y - 1f),
                    AcidGreenBright * alpha, 0.5f);
            }
        }

        public void DrawWidgetDivider(SpriteBatch sb, Vector2 start, Vector2 end, float alpha) {
            var px = VaultAsset.placeholder2.Value;
            float len = (end - start).Length();
            if (len < 1f) return;
            Vector2 dir = (end - start) / len;
            Vector2 nrm = new(-dir.Y, dir.X);
            float rot = MathF.Atan2(dir.Y, dir.X);

            //每7px一气泡点
            int k = 0;
            for (float c = 0; c < len; c += 7f) {
                float t = c / len;
                float yOff = MathF.Sin(bubble * 1.6f + k * 0.9f) * 1.1f;
                Vector2 pos = start + dir * c + nrm * yOff;
                float fade = MathF.Sin(t * MathHelper.Pi) * 0.55f + 0.18f;
                sb.Draw(px, pos, new Rectangle(0, 0, 1, 1),
                    AcidGreenDim * (alpha * fade), rot, new Vector2(0, 0.5f),
                    new Vector2(1f, 1f), SpriteEffects.None, 0f);
                k++;
            }
        }

        public void DrawWidgetOverlay(SpriteBatch sb, Rectangle rect, float alpha) { }

        public Color GetWidgetTitleColor(float alpha) => TitleWarm * alpha;
        public Color GetWidgetTextColor(float alpha) => TextBody * (alpha * 0.95f);
        public Color GetWidgetAccentColor(float alpha) => AcidGreen * alpha;

        public int? GetPreferredWidth() => 240;
        public int? GetMinHeight() => 62;
        public int? GetIdleCompactHeight(EntrustEntryData entry) {
            //待机双行紧凑
            if (entry.Progress <= 0f && entry.Status != QuestEntryStatus.Completed)
                return 100;
            return null;
        }
    }
}

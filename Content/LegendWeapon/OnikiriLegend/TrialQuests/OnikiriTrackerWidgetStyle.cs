using CalamityOverhaul.Content.EntrustManager;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.UI;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.TrialQuests
{
    /// <summary>鬼切追踪窗,朱印+刀痕下划线+细进度条</summary>
    internal class OnikiriTrackerWidgetStyle : IEntrustTrackerWidgetStyle
    {
        private float pulse;

        public void Update(Rectangle widgetRect, float slideProgress) {
            pulse += 0.035f;
            if (pulse > MathHelper.TwoPi) {
                pulse -= MathHelper.TwoPi;
            }
        }

        public void Reset() => pulse = 0f;

        public void DrawWidgetBackground(SpriteBatch sb, Rectangle rect, float alpha) { }

        public void DrawWidgetFrame(SpriteBatch sb, Rectangle rect, float alpha) { }

        public void DrawWidgetHeader(SpriteBatch sb, Rectangle headerRect, string title, float alpha) {
            var px = VaultAsset.placeholder2.Value;
            var uv = new Rectangle(0, 0, 1, 1);
            var font = FontAssets.MouseText.Value;

            float sealPulse = MathF.Sin(pulse * 2f) * 0.12f + 0.88f;
            OniBrush.DrawSealGlyph(sb,
                new Vector2(headerRect.X + 10f, headerRect.Y + headerRect.Height * 0.5f),
                10f, alpha * sealPulse);

            const float titleScale = 0.95f;
            int textX = headerRect.X + 22;
            float textY = headerRect.Y + (headerRect.Height - 16f) / 2f;
            Vector2 titlePos = new(textX, textY);

            Utils.DrawBorderString(sb, title, titlePos + new Vector2(0, 1),
                OnikiriUITheme.Ink * (alpha * 0.55f), titleScale);
            Utils.DrawBorderString(sb, title, titlePos, OnikiriUITheme.HotWhite * alpha, titleScale);

            int titlePixelW = (int)(font.MeasureString(title).X * titleScale);
            int underY = headerRect.Bottom + 1;
            int solidLen = Math.Clamp(titlePixelW + 4, 18, headerRect.Width - 28);
            float p = MathF.Sin(pulse * 1.8f) * 0.18f + 0.82f;

            OniBrush.DrawTaperedSlash(sb,
                new Vector2(textX, underY),
                new Vector2(textX + solidLen, underY + 0.5f),
                1.5f, 0.35f, alpha * 0.85f * p);

            int dotStart = textX + solidLen + 4;
            int dotEnd = headerRect.Right - 8;
            for (int x = dotStart; x < dotEnd; x += 4) {
                int w = Math.Min(2, dotEnd - x);
                if (w <= 0) {
                    break;
                }
                float t = (float)(x - dotStart) / Math.Max(1, dotEnd - dotStart);
                sb.Draw(px, new Rectangle(x, underY, w, 1), uv, OnikiriUITheme.Deep * (alpha * (1f - t) * 0.55f));
            }
        }

        public void DrawWidgetProgress(SpriteBatch sb, Rectangle barRect, float progress,
            string progressText, float alpha) {
            var px = VaultAsset.placeholder2.Value;
            var uv = new Rectangle(0, 0, 1, 1);

            const int barH = 2;
            int y = barRect.Y + (barRect.Height - barH) / 2;
            int trackW = barRect.Width;

            sb.Draw(px, new Rectangle(barRect.X, y, trackW, barH), uv, OnikiriUITheme.Dark * (alpha * 0.35f));

            int fillW = (int)(trackW * MathHelper.Clamp(progress, 0f, 1f));
            if (fillW > 0) {
                sb.Draw(px, new Rectangle(barRect.X, y, fillW, barH), uv, OnikiriUITheme.Bright * (alpha * 0.92f));
                if (fillW > 1) {
                    sb.Draw(px, new Rectangle(barRect.X + fillW - 1, y - 1, 1, barH + 2), uv,
                        OnikiriUITheme.HotWhite * (alpha * 0.55f));
                }
            }

            for (int i = 1; i < 4; i++) {
                int tx = barRect.X + (int)(trackW * (i / 4f));
                sb.Draw(px, new Rectangle(tx, y + barH, 1, 2), uv, OnikiriUITheme.Deep * (alpha * 0.4f));
            }

            if (!string.IsNullOrEmpty(progressText)) {
                var font = FontAssets.MouseText.Value;
                Vector2 sz = font.MeasureString(progressText) * 0.5f;
                Utils.DrawBorderString(sb, progressText,
                    new Vector2(barRect.Right - sz.X - 1f, y - sz.Y - 1f),
                    OnikiriUITheme.Bright * alpha, 0.5f);
            }
        }

        public void DrawWidgetDivider(SpriteBatch sb, Vector2 start, Vector2 end, float alpha) {
            OniBrush.DrawTaperedSlash(sb, start, end, 1.2f, 0.3f, alpha * 0.4f);
        }

        public void DrawWidgetOverlay(SpriteBatch sb, Rectangle rect, float alpha) { }

        public Color GetWidgetTitleColor(float alpha) => OnikiriUITheme.HotWhite * alpha;
        public Color GetWidgetTextColor(float alpha) => OnikiriUITheme.Paper * (alpha * 0.95f);
        public Color GetWidgetAccentColor(float alpha) => OnikiriUITheme.Bright * alpha;

        public int? GetPreferredWidth() => 240;
        public int? GetMinHeight() => 62;
        public int? GetIdleCompactHeight(EntrustEntryData entry) {
            if (entry.Progress <= 0f && entry.Status != QuestEntryStatus.Completed) {
                return 70;
            }
            return null;
        }
    }
}

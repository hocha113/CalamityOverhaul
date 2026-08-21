using CalamityOverhaul.Content.EntrustManager;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaVaults;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.TrialQuests
{
    /// <summary>鬼伞追踪窗:墨滴印+湿线下划+水位式进度条,配色走血湖(KikasaVaultTheme)</summary>
    internal class KikasaTrackerWidgetStyle : IEntrustTrackerWidgetStyle
    {
        private float pulse;

        public void Update(Rectangle widgetRect, float slideProgress) {
            pulse += 0.03f;
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

            //墨滴印:一粒悬滴,呼吸明灭
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            Vector2 sealPos = new(headerRect.X + 10f, headerRect.Y + headerRect.Height * 0.5f);
            float sealPulse = MathF.Sin(pulse * 2f) * 0.14f + 0.86f;
            if (glow != null) {
                Color core = KikasaVaultTheme.Blood with { A = 0 };
                sb.Draw(glow, sealPos, null, core * (alpha * 0.7f * sealPulse), 0f,
                    glow.Size() * 0.5f, 22f / glow.Width, SpriteEffects.None, 0f);
                sb.Draw(glow, sealPos, null, (Color.White with { A = 0 }) * (alpha * 0.3f * sealPulse), 0f,
                    glow.Size() * 0.5f, 9f / glow.Width, SpriteEffects.None, 0f);
            }
            //滴尾:印下坠一线,像悬而未落的滴
            int tailY = (int)(sealPos.Y + 4f);
            sb.Draw(px, new Rectangle((int)sealPos.X, tailY, 1, 4), uv,
                KikasaVaultTheme.Blood * (alpha * 0.6f * sealPulse));

            const float titleScale = 0.95f;
            int textX = headerRect.X + 22;
            float textY = headerRect.Y + (headerRect.Height - 16f) / 2f;
            Vector2 titlePos = new(textX, textY);

            Utils.DrawBorderString(sb, title, titlePos + new Vector2(0, 1),
                KikasaVaultTheme.Deep * (alpha * 0.6f), titleScale);
            Utils.DrawBorderString(sb, title, titlePos, KikasaVaultTheme.Text * alpha, titleScale);

            //湿线下划:实段之后接一串渐稀的落滴点
            int titlePixelW = (int)(font.MeasureString(title).X * titleScale);
            int underY = headerRect.Bottom + 1;
            int solidLen = Math.Clamp(titlePixelW + 4, 18, headerRect.Width - 28);
            float p = MathF.Sin(pulse * 1.6f) * 0.16f + 0.84f;
            sb.Draw(px, new Rectangle(textX, underY, solidLen, 1), uv,
                KikasaVaultTheme.Blood * (alpha * 0.8f * p));

            int dotStart = textX + solidLen + 5;
            int dotEnd = headerRect.Right - 8;
            int step = 0;
            for (int x = dotStart; x < dotEnd; x += 6, step++) {
                float t = (float)(x - dotStart) / Math.Max(1, dotEnd - dotStart);
                //竖向短滴,越远越沉越淡
                int drop = 1 + (step % 3 == 0 ? 1 : 0);
                sb.Draw(px, new Rectangle(x, underY + step % 2, 1, drop), uv,
                    KikasaVaultTheme.Foam * (alpha * (1f - t) * 0.5f));
            }
        }

        public void DrawWidgetProgress(SpriteBatch sb, Rectangle barRect, float progress,
            string progressText, float alpha) {
            var px = VaultAsset.placeholder2.Value;
            var uv = new Rectangle(0, 0, 1, 1);

            const int barH = 2;
            int y = barRect.Y + (barRect.Height - barH) / 2;
            int trackW = barRect.Width;

            sb.Draw(px, new Rectangle(barRect.X, y, trackW, barH), uv,
                KikasaVaultTheme.Mid * (alpha * 0.45f));

            int fillW = (int)(trackW * MathHelper.Clamp(progress, 0f, 1f));
            if (fillW > 0) {
                sb.Draw(px, new Rectangle(barRect.X, y, fillW, barH), uv,
                    KikasaVaultTheme.Blood * (alpha * 0.92f));
                if (fillW > 1) {
                    //水线前端:泡沫亮头
                    sb.Draw(px, new Rectangle(barRect.X + fillW - 1, y - 1, 1, barH + 2), uv,
                        KikasaVaultTheme.Foam * (alpha * 0.65f));
                }
            }

            //四分位水位刻度
            for (int i = 1; i < 4; i++) {
                int tx = barRect.X + (int)(trackW * (i / 4f));
                sb.Draw(px, new Rectangle(tx, y + barH, 1, 2), uv,
                    KikasaVaultTheme.Deep * (alpha * 0.5f));
            }

            if (!string.IsNullOrEmpty(progressText)) {
                var font = FontAssets.MouseText.Value;
                Vector2 sz = font.MeasureString(progressText) * 0.5f;
                Utils.DrawBorderString(sb, progressText,
                    new Vector2(barRect.Right - sz.X - 1f, y - sz.Y - 1f),
                    KikasaVaultTheme.Foam * alpha, 0.5f);
            }
        }

        public void DrawWidgetDivider(SpriteBatch sb, Vector2 start, Vector2 end, float alpha) {
            var px = VaultAsset.placeholder2.Value;
            var uv = new Rectangle(0, 0, 1, 1);
            //一条渐淡的湿痕
            int len = (int)Vector2.Distance(start, end);
            for (int x = 0; x < len; x += 3) {
                float t = x / (float)Math.Max(1, len);
                sb.Draw(px, new Rectangle((int)start.X + x, (int)start.Y, 2, 1), uv,
                    KikasaVaultTheme.Blood * (alpha * 0.4f * (1f - t * 0.7f)));
            }
        }

        public void DrawWidgetOverlay(SpriteBatch sb, Rectangle rect, float alpha) { }

        public Color GetWidgetTitleColor(float alpha) => KikasaVaultTheme.Text * alpha;
        public Color GetWidgetTextColor(float alpha) => KikasaVaultTheme.TextDim * (alpha * 0.95f);
        public Color GetWidgetAccentColor(float alpha) => KikasaVaultTheme.Blood * alpha;

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

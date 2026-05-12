using CalamityOverhaul.Content.ADV.EntrustManager;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;

namespace CalamityOverhaul.Content.ADV.Scenarios.SupCal.Quest
{
    /// <summary>
    /// 硫火女巫委托追踪窗口样式——极简地狱火HUD：<br/>
    /// 完全无背景与外框，标题左侧是"上升火焰三角 + 顶端余烬"记号，
    /// 标题下方是实线 + 三粒错相闪烁的余烬点，
    /// 进度仅以贴近文字的 2px 火色细线呈现，保留炽热感而不堆叠面板。
    /// </summary>
    internal class BrimstoneTrackerWidgetStyle : IEntrustTrackerWidgetStyle
    {
        #region 色板

        private static readonly Color FireRed = new(220, 80, 30);
        private static readonly Color FireRedBright = new(255, 150, 70);
        private static readonly Color FireRedDim = new(120, 35, 15);
        private static readonly Color EmberGold = new(255, 195, 110);
        private static readonly Color TitleWarm = new(255, 220, 180);
        private static readonly Color TextBody = new(225, 190, 165);
        private static readonly Color ShadowInk = new(14, 4, 2);

        #endregion

        private float pulse;
        private float flicker;

        public void Update(Rectangle widgetRect, float slideProgress) {
            pulse += 0.034f;
            flicker += 0.07f;
            if (pulse > MathHelper.TwoPi) pulse -= MathHelper.TwoPi;
            if (flicker > MathHelper.TwoPi) flicker -= MathHelper.TwoPi;
        }

        public void Reset() { pulse = 0f; flicker = 0f; }

        public void DrawWidgetBackground(SpriteBatch sb, Rectangle rect, float alpha) { }
        public void DrawWidgetFrame(SpriteBatch sb, Rectangle rect, float alpha) { }

        public void DrawWidgetHeader(SpriteBatch sb, Rectangle headerRect, string title, float alpha) {
            var px = VaultAsset.placeholder2.Value;
            var uv = new Rectangle(0, 0, 1, 1);
            var font = FontAssets.MouseText.Value;

            //头部记号——上升火焰三角 ∧ + 顶端余烬粒
            int markX = headerRect.X + 8;
            int markY = headerRect.Y + headerRect.Height / 2;
            DrawFlameMark(sb, px, uv, markX, markY, alpha);

            //标题文字——红色拖尾投影 + 主体暖白（字号略大于默认正文）
            const float titleScale = 0.95f;
            int textX = headerRect.X + 20;
            //大字号下需略微下移基线，让顶部不贴顶
            float textY = headerRect.Y + (headerRect.Height - 16f) / 2f;
            Vector2 titlePos = new(textX, textY);

            //侧偏火色虚影 + 深色投影 + 主体（模拟热浪烘焙感）
            Utils.DrawBorderString(sb, title, titlePos + new Vector2(1, 1),
                FireRedDim * (alpha * 0.45f), titleScale);
            Utils.DrawBorderString(sb, title, titlePos + new Vector2(0, 1),
                ShadowInk * (alpha * 0.55f), titleScale);
            Utils.DrawBorderString(sb, title, titlePos, TitleWarm * alpha, titleScale);

            //下划线——实线 + 三粒错相闪烁的余烬点（下移避开放大后的标题底部）
            int titlePixelW = (int)(font.MeasureString(title).X * titleScale);
            int underY = headerRect.Bottom + 1;
            int solidLen = Math.Clamp(titlePixelW + 4, 20, headerRect.Width - 38);
            float p = MathF.Sin(pulse * 2f) * 0.2f + 0.8f;

            sb.Draw(px, new Rectangle(textX, underY, solidLen, 1), uv, FireRed * (alpha * 0.88f * p));

            //三粒余烬——各自相位错开闪烁，y方向微抖
            int emberStart = textX + solidLen + 5;
            for (int k = 0; k < 3; k++) {
                int ex = emberStart + k * 6;
                if (ex > headerRect.Right - 6) break;
                float phase = flicker * 1.3f + k * 1.7f;
                float fade = MathF.Sin(phase) * 0.45f + 0.5f;
                int ey = underY + (int)(MathF.Sin(phase * 1.6f + 0.3f) * 1.2f);
                int w = (k == 1) ? 2 : 1; //中间那粒稍大
                sb.Draw(px, new Rectangle(ex, ey, w, 1), uv,
                    EmberGold * (alpha * (0.25f + fade * 0.6f)));
            }
        }

        //火焰三角 ∧：两条45°斜线交于上方，外加顶端1px余烬粒
        private void DrawFlameMark(SpriteBatch sb, Texture2D px, Rectangle uv, int cx, int cy, float alpha) {
            float p = MathF.Sin(pulse * 2.4f) * 0.22f + 0.78f;
            Color body = FireRedBright * (alpha * p);
            Color shadow = ShadowInk * (alpha * 0.4f);

            //投影（偏移1px向下）
            sb.Draw(px, new Vector2(cx - 4, cy + 3), uv, shadow,
                -MathHelper.PiOver4, new Vector2(0f, 0.5f),
                new Vector2(6f, 1f), SpriteEffects.None, 0f);
            sb.Draw(px, new Vector2(cx + 4, cy + 3), uv, shadow,
                MathHelper.PiOver4 - MathHelper.Pi, new Vector2(0f, 0.5f),
                new Vector2(6f, 1f), SpriteEffects.None, 0f);

            //左斜（从左下向右上→指向顶点）
            sb.Draw(px, new Vector2(cx - 4, cy + 2), uv, body,
                -MathHelper.PiOver4, new Vector2(0f, 0.5f),
                new Vector2(6f, 1f), SpriteEffects.None, 0f);
            //右斜（从右下向左上→指向顶点）
            sb.Draw(px, new Vector2(cx + 4, cy + 2), uv, body,
                MathHelper.PiOver4 - MathHelper.Pi, new Vector2(0f, 0.5f),
                new Vector2(6f, 1f), SpriteEffects.None, 0f);

            //顶端余烬粒——竖向漂浮，亮度随flicker明灭
            float embPhase = flicker * 1.1f;
            float ember = MathF.Sin(embPhase) * 0.5f + 0.5f;
            float embY = cy - 5f - ember * 1.6f;
            sb.Draw(px, new Rectangle(cx, (int)embY, 1, 1), uv,
                EmberGold * (alpha * (0.55f + ember * 0.45f)));
        }

        public void DrawWidgetProgress(SpriteBatch sb, Rectangle barRect, float progress,
            string progressText, float alpha) {
            var px = VaultAsset.placeholder2.Value;
            var uv = new Rectangle(0, 0, 1, 1);

            //2px火色细线
            const int barH = 2;
            int y = barRect.Y + (barRect.Height - barH) / 2;
            int trackW = barRect.Width;

            //轨道——暗红底线
            sb.Draw(px, new Rectangle(barRect.X, y, trackW, barH), uv, FireRedDim * (alpha * 0.45f));

            //填充——深红→余烬金的渐变
            int fillW = (int)(trackW * MathHelper.Clamp(progress, 0f, 1f));
            if (fillW > 0) {
                int segs = Math.Max(6, fillW / 4);
                for (int i = 0; i < segs; i++) {
                    float t = i / (float)segs;
                    float t2 = (i + 1) / (float)segs;
                    int x1 = barRect.X + (int)(t * fillW);
                    int x2 = barRect.X + (int)(t2 * fillW);
                    int w = Math.Max(1, x2 - x1);
                    Color c = Color.Lerp(FireRed, EmberGold, t * 0.75f) * (alpha * 0.95f);
                    sb.Draw(px, new Rectangle(x1, y, w, barH), uv, c);
                }
                //尖端炽亮余烬
                if (fillW > 1) {
                    sb.Draw(px, new Rectangle(barRect.X + fillW - 1, y - 1, 1, barH + 2), uv,
                        FireRedBright * (alpha * 0.85f));
                }
                //尖端上方"余烬火花"——1px小点，向上偏移2px，随flicker相位明灭
                float sparkFade = MathF.Sin(flicker * 1.5f) * 0.5f + 0.5f;
                if (sparkFade > 0.05f) {
                    sb.Draw(px, new Rectangle(barRect.X + fillW - 1, y - 3, 1, 1), uv,
                        EmberGold * (alpha * 0.7f * sparkFade));
                }
            }

            //满级——条带整体微微脉动出余烬光
            if (progress >= 0.999f) {
                float fp = MathF.Sin(pulse * 4f) * 0.5f + 0.5f;
                sb.Draw(px, new Rectangle(barRect.X, y, trackW, barH), uv,
                    EmberGold * (alpha * 0.18f * fp));
            }

            //进度文字——靠右上方，0.5倍小字
            if (!string.IsNullOrEmpty(progressText)) {
                var font = FontAssets.MouseText.Value;
                Vector2 sz = font.MeasureString(progressText) * 0.5f;
                Utils.DrawBorderString(sb, progressText,
                    new Vector2(barRect.Right - sz.X - 1f, y - sz.Y - 1f),
                    EmberGold * alpha, 0.5f);
            }
        }

        public void DrawWidgetDivider(SpriteBatch sb, Vector2 start, Vector2 end, float alpha) {
            var px = VaultAsset.placeholder2.Value;
            float len = (end - start).Length();
            if (len < 1f) return;
            Vector2 dir = (end - start) / len;
            float rot = MathF.Atan2(dir.Y, dir.X);

            //不规则火焰节律：1~3px变长度，亮度随相位起伏
            int k = 0;
            for (float c = 0; c < len; c += 5f) {
                float t = c / len;
                float jitter = MathF.Sin(flicker * 0.9f + k * 1.4f) * 0.5f + 0.5f;
                float segLen = Math.Min(1f + jitter * 2f, len - c);
                float fade = (MathF.Sin(flicker + k * 0.7f) * 0.35f + 0.45f) * (1f - t * 0.5f);
                sb.Draw(px, start + dir * c, new Rectangle(0, 0, 1, 1),
                    FireRedDim * (alpha * fade), rot, new Vector2(0, 0.5f),
                    new Vector2(segLen, 1f), SpriteEffects.None, 0f);
                k++;
            }
        }

        public void DrawWidgetOverlay(SpriteBatch sb, Rectangle rect, float alpha) { }

        public Color GetWidgetTitleColor(float alpha) => TitleWarm * alpha;
        public Color GetWidgetTextColor(float alpha) => TextBody * (alpha * 0.95f);
        public Color GetWidgetAccentColor(float alpha) => EmberGold * alpha;

        public int? GetPreferredWidth() => 240;
        public int? GetMinHeight() => 62;
        public int? GetIdleCompactHeight(EntrustEntryData entry) {
            //待机时折叠成"标题 + 下划线 + 描述 + 等待提示"的双行紧凑布局
            if (entry.Progress <= 0f && entry.Status != QuestEntryStatus.Completed)
                return 70;
            return null;
        }
    }
}

using CalamityOverhaul.Content.ADV.EntrustManager;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;

namespace CalamityOverhaul.Content.ADV.Scenarios.Draedons.Quest
{
    /// <summary>
    /// 嘉登委托追踪窗口样式——极简数据终端HUD：<br/>
    /// 完全无背景与外框，标题左侧是"四角传感目镜 + 中央数据微粒"，
    /// 标题下方是实线 + 末端菱形数据头 + 后段摩斯节律细线，
    /// 进度仅以贴近文字的 2px 青蓝细线呈现，避免在屏幕侧边形成厚重面板。
    /// </summary>
    internal class DraedonTrackerWidgetStyle : IEntrustTrackerWidgetStyle
    {
        #region 色板

        private static readonly Color DataCyan = new(95, 195, 240);
        private static readonly Color DataCyanBright = new(170, 235, 255);
        private static readonly Color DataCyanDim = new(35, 100, 160);
        private static readonly Color AccentTeal = new(80, 255, 220);
        private static readonly Color TitleSky = new(205, 235, 250);
        private static readonly Color TextSky = new(180, 215, 235);
        private static readonly Color ShadowInk = new(2, 6, 14);

        #endregion

        private float pulse;
        private float scan;

        public void Update(Rectangle widgetRect, float slideProgress) {
            pulse += 0.032f;
            scan += 0.055f;
            if (pulse > MathHelper.TwoPi) pulse -= MathHelper.TwoPi;
            if (scan > MathHelper.TwoPi) scan -= MathHelper.TwoPi;
        }

        public void Reset() { pulse = 0f; scan = 0f; }

        //极简：不绘制背景
        public void DrawWidgetBackground(SpriteBatch sb, Rectangle rect, float alpha) { }

        //极简：不绘制外框
        public void DrawWidgetFrame(SpriteBatch sb, Rectangle rect, float alpha) { }

        public void DrawWidgetHeader(SpriteBatch sb, Rectangle headerRect, string title, float alpha) {
            var px = VaultAsset.placeholder2.Value;
            var uv = new Rectangle(0, 0, 1, 1);
            var font = FontAssets.MouseText.Value;

            //头部记号——四角传感目镜（4个L形角点 + 中央脉冲数据粒）
            int markX = headerRect.X + 7;
            int markY = headerRect.Y + headerRect.Height / 2;
            DrawSensorReticle(sb, px, uv, markX, markY, alpha);

            //标题文字——深色投影 + 主体清亮蓝（字号略大于默认正文）
            const float titleScale = 0.95f;
            int textX = headerRect.X + 20;
            //大字号下需略微下移基线，让顶部不贴顶
            float textY = headerRect.Y + (headerRect.Height - 16f) / 2f;
            Vector2 titlePos = new(textX, textY);

            Utils.DrawBorderString(sb, title, titlePos + new Vector2(0, 1), ShadowInk * (alpha * 0.55f), titleScale);
            Utils.DrawBorderString(sb, title, titlePos, TitleSky * alpha, titleScale);

            //下划线——实线 + 末端小菱形数据头 + 后段摩斯节律（下移避开放大后的标题底部）
            int titlePixelW = (int)(font.MeasureString(title).X * titleScale);
            int underY = headerRect.Bottom + 1;
            int solidLen = Math.Clamp(titlePixelW + 4, 20, headerRect.Width - 40);
            float p = MathF.Sin(pulse * 2f) * 0.18f + 0.82f;

            sb.Draw(px, new Rectangle(textX, underY, solidLen, 1), uv, DataCyan * (alpha * 0.85f * p));

            //末端小菱形——青绿色"数据头"标记
            int diaCx = textX + solidLen + 3;
            sb.Draw(px, new Vector2(diaCx, underY + 0.5f), uv,
                AccentTeal * (alpha * p), MathHelper.PiOver4,
                new Vector2(0.5f), new Vector2(2.6f), SpriteEffects.None, 0f);

            //后段摩斯节律——点（1px）/划（2px）交替，逐渐淡出
            int patternStart = diaCx + 5;
            int patternEnd = headerRect.Right - 8;
            int xp = patternStart;
            int idx = 0;
            while (xp < patternEnd) {
                int segW = (idx % 2 == 0) ? 1 : 2;
                if (xp + segW > patternEnd) break;
                float t = (float)(xp - patternStart) / Math.Max(1, patternEnd - patternStart);
                float fade = (1f - t) * 0.65f;
                sb.Draw(px, new Rectangle(xp, underY, segW, 1), uv, DataCyanDim * (alpha * fade));
                xp += segW + 2;
                idx++;
            }
        }

        //四角传感目镜：4个L形角点框住一个7x7区域 + 中央1px数据粒
        private void DrawSensorReticle(SpriteBatch sb, Texture2D px, Rectangle uv, int cx, int cy, float alpha) {
            float p = MathF.Sin(pulse * 2.2f) * 0.25f + 0.75f;
            Color corner = DataCyanBright * (alpha * p);
            const int s = 3;
            const int tickLen = 2;

            //左上 ⌐
            sb.Draw(px, new Rectangle(cx - s, cy - s, tickLen, 1), uv, corner);
            sb.Draw(px, new Rectangle(cx - s, cy - s, 1, tickLen), uv, corner);
            //右上 ¬
            sb.Draw(px, new Rectangle(cx + s - tickLen + 1, cy - s, tickLen, 1), uv, corner);
            sb.Draw(px, new Rectangle(cx + s, cy - s, 1, tickLen), uv, corner);
            //左下 ⌊
            sb.Draw(px, new Rectangle(cx - s, cy + s, tickLen, 1), uv, corner);
            sb.Draw(px, new Rectangle(cx - s, cy + s - tickLen + 1, 1, tickLen), uv, corner);
            //右下 ⌋
            sb.Draw(px, new Rectangle(cx + s - tickLen + 1, cy + s, tickLen, 1), uv, corner);
            sb.Draw(px, new Rectangle(cx + s, cy + s - tickLen + 1, 1, tickLen), uv, corner);

            //中央数据粒——根据扫描相位脉冲
            float corePulse = MathF.Sin(scan * 1.4f) * 0.5f + 0.5f;
            sb.Draw(px, new Rectangle(cx, cy, 1, 1), uv,
                AccentTeal * (alpha * (0.5f + corePulse * 0.5f)));
        }

        public void DrawWidgetProgress(SpriteBatch sb, Rectangle barRect, float progress,
            string progressText, float alpha) {
            var px = VaultAsset.placeholder2.Value;
            var uv = new Rectangle(0, 0, 1, 1);

            //超扁平 2px 进度细线
            const int barH = 2;
            int y = barRect.Y + (barRect.Height - barH) / 2;
            int trackW = barRect.Width;

            //轨道——极淡的暗蓝底线
            sb.Draw(px, new Rectangle(barRect.X, y, trackW, barH), uv, DataCyanDim * (alpha * 0.24f));

            //填充——蓝→青绿的轻微数据流渐变
            int fillW = (int)(trackW * MathHelper.Clamp(progress, 0f, 1f));
            if (fillW > 0) {
                int segs = Math.Max(6, fillW / 4);
                for (int i = 0; i < segs; i++) {
                    float t = i / (float)segs;
                    float t2 = (i + 1) / (float)segs;
                    int x1 = barRect.X + (int)(t * fillW);
                    int x2 = barRect.X + (int)(t2 * fillW);
                    int w = Math.Max(1, x2 - x1);
                    Color c = Color.Lerp(DataCyan, AccentTeal, t * 0.7f) * (alpha * 0.92f);
                    sb.Draw(px, new Rectangle(x1, y, w, barH), uv, c);
                }
                //尖端扫描头——向上下延伸 1px 的白色高亮
                if (fillW > 1) {
                    sb.Draw(px, new Rectangle(barRect.X + fillW - 1, y - 1, 1, barH + 2), uv,
                        Color.White * (alpha * 0.7f));
                }
            }

            //四分位刻度（向下凸出 1px）
            for (int i = 1; i < 4; i++) {
                int tx = barRect.X + (int)(trackW * (i / 4f));
                sb.Draw(px, new Rectangle(tx, y + barH, 1, 2), uv, DataCyanDim * (alpha * 0.45f));
            }

            //进度文字——靠右上方，0.5倍小字
            if (!string.IsNullOrEmpty(progressText)) {
                var font = FontAssets.MouseText.Value;
                Vector2 sz = font.MeasureString(progressText) * 0.5f;
                Utils.DrawBorderString(sb, progressText,
                    new Vector2(barRect.Right - sz.X - 1f, y - sz.Y - 1f),
                    AccentTeal * alpha, 0.5f);
            }
        }

        public void DrawWidgetDivider(SpriteBatch sb, Vector2 start, Vector2 end, float alpha) {
            var px = VaultAsset.placeholder2.Value;
            float len = (end - start).Length();
            if (len < 1f) return;
            Vector2 dir = (end - start) / len;
            float rot = MathF.Atan2(dir.Y, dir.X);

            //机械节律：3px短划 + 3px间隔
            for (float c = 0; c < len; c += 6f) {
                float segLen = Math.Min(3f, len - c);
                sb.Draw(px, start + dir * c, new Rectangle(0, 0, 1, 1),
                    DataCyanDim * (alpha * 0.36f), rot, new Vector2(0, 0.5f),
                    new Vector2(segLen, 1f), SpriteEffects.None, 0f);
            }
        }

        //极简：不绘制覆盖特效
        public void DrawWidgetOverlay(SpriteBatch sb, Rectangle rect, float alpha) { }

        public Color GetWidgetTitleColor(float alpha) => TitleSky * alpha;
        public Color GetWidgetTextColor(float alpha) => TextSky * (alpha * 0.95f);
        public Color GetWidgetAccentColor(float alpha) => AccentTeal * alpha;

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

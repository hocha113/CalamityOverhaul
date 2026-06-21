using CalamityOverhaul.Content.EntrustManager;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;

namespace CalamityOverhaul.Content.LegendWeapon.MurasamaLegend.TrialQuests
{
    /// <summary>
    /// 鬼妖村正追踪窗口，MGR:R血刃极简HUD<br/>
    /// 无背景外框，标题左刀脊竖记、下实线箭头分隔，进度细红线贴字
    /// </summary>
    internal class PhantomTrackerWidgetStyle : IEntrustTrackerWidgetStyle
    {
        #region 色板

        private static readonly Color BladeRed = new(240, 80, 80);
        private static readonly Color BladeRedBright = new(255, 130, 110);
        private static readonly Color BladeRedDim = new(150, 28, 36);
        private static readonly Color CrimsonInk = new(80, 8, 12);
        private static readonly Color TitleBlade = new(245, 220, 220);
        private static readonly Color TextBlade = new(220, 195, 195);
        private static readonly Color ShadowInk = new(8, 2, 4);

        #endregion

        private float pulse;
        private float scan;

        public void Update(Rectangle widgetRect, float slideProgress) {
            pulse += 0.034f;
            scan += 0.05f;
            if (pulse > MathHelper.TwoPi) pulse -= MathHelper.TwoPi;
            if (scan > MathHelper.TwoPi) scan -= MathHelper.TwoPi;
        }

        public void Reset() { pulse = 0f; scan = 0f; }

        public void DrawWidgetBackground(SpriteBatch sb, Rectangle rect, float alpha) { }
        public void DrawWidgetFrame(SpriteBatch sb, Rectangle rect, float alpha) { }

        public void DrawWidgetHeader(SpriteBatch sb, Rectangle headerRect, string title, float alpha) {
            var px = VaultAsset.placeholder2.Value;
            var uv = new Rectangle(0, 0, 1, 1);
            var font = FontAssets.MouseText.Value;

            //头部刀脊粗竖条+顶端斜切
            DrawBladeMark(sb, px, uv, headerRect.X + 6, headerRect.Y + 4, headerRect.Height - 8, alpha);

            //标题血色拖尾+银白主体，字号略大
            const float titleScale = 0.95f;
            int textX = headerRect.X + 18;
            //大字号下需略微下移基线，让顶部不贴顶
            float textY = headerRect.Y + (headerRect.Height - 16f) / 2f;
            Vector2 titlePos = new(textX, textY);

            //2px侧偏的血色虚影（模拟刀斩残像）
            Utils.DrawBorderString(sb, title, titlePos + new Vector2(2, 1),
                BladeRedDim * (alpha * 0.55f), titleScale);
            Utils.DrawBorderString(sb, title, titlePos + new Vector2(0, 1),
                ShadowInk * (alpha * 0.5f), titleScale);
            Utils.DrawBorderString(sb, title, titlePos, TitleBlade * alpha, titleScale);

            //下划实线+箭头+后段虚线，下移避标题底
            int titlePixelW = (int)(font.MeasureString(title).X * titleScale);
            int underY = headerRect.Bottom + 1;
            int solidLen = Math.Clamp(titlePixelW + 4, 22, headerRect.Width - 36);
            float p = MathF.Sin(pulse * 2f) * 0.18f + 0.82f;

            sb.Draw(px, new Rectangle(textX, underY, solidLen, 1), uv, BladeRed * (alpha * 0.85f * p));
            //三角箭头：用一根斜短线 + 一根反向斜短线在末端拼成 ▶
            int arrowX = textX + solidLen + 1;
            DrawSmallArrow(sb, px, uv, arrowX, underY, alpha * p);

            //箭头之后：稀疏的血点虚线
            int dotStart = arrowX + 6;
            int dotEnd = headerRect.Right - 8;
            for (int x = dotStart; x < dotEnd; x += 5) {
                int w = Math.Min(2, dotEnd - x);
                if (w <= 0) break;
                float t = (float)(x - dotStart) / Math.Max(1, dotEnd - dotStart);
                float fade = (1f - t) * 0.6f;
                sb.Draw(px, new Rectangle(x, underY, w, 1), uv, CrimsonInk * (alpha * fade));
            }
        }

        //刀脊：3px粗竖条 + 1px斜切刀尖
        private void DrawBladeMark(SpriteBatch sb, Texture2D px, Rectangle uv, int x, int y, int h, float alpha) {
            float p = MathF.Sin(pulse * 2.4f) * 0.22f + 0.78f;
            //投影
            sb.Draw(px, new Rectangle(x + 1, y + 1, 3, h), uv, ShadowInk * (alpha * 0.4f));
            //主体粗竖条
            sb.Draw(px, new Rectangle(x, y, 3, h), uv, BladeRed * (alpha * p));
            //中央高光1px
            sb.Draw(px, new Rectangle(x + 1, y + 1, 1, h - 2), uv, BladeRedBright * (alpha * 0.7f * p));
            //顶端刀尖：在主体上方斜出一个2px小尖
            sb.Draw(px, new Vector2(x + 1.5f, y), uv, BladeRedBright * (alpha * 0.9f * p),
                MathHelper.PiOver4, new Vector2(0.5f, 1f), new Vector2(2.4f, 1f), SpriteEffects.None, 0f);
        }

        //小三角箭头 ▶
        private void DrawSmallArrow(SpriteBatch sb, Texture2D px, Rectangle uv, int x, int y, float alpha) {
            //顶斜
            sb.Draw(px, new Vector2(x, y), uv, BladeRedBright * (alpha * 0.9f),
                -MathHelper.PiOver4, new Vector2(0f, 0.5f), new Vector2(4f, 1f), SpriteEffects.None, 0f);
            //底斜
            sb.Draw(px, new Vector2(x, y + 1), uv, BladeRedBright * (alpha * 0.9f),
                MathHelper.PiOver4, new Vector2(0f, 0.5f), new Vector2(4f, 1f), SpriteEffects.None, 0f);
        }

        public void DrawWidgetProgress(SpriteBatch sb, Rectangle barRect, float progress,
            string progressText, float alpha) {
            var px = VaultAsset.placeholder2.Value;
            var uv = new Rectangle(0, 0, 1, 1);

            //2px红色细线
            const int barH = 2;
            int y = barRect.Y + (barRect.Height - barH) / 2;
            int trackW = barRect.Width;

            //轨道血色暗线
            sb.Draw(px, new Rectangle(barRect.X, y, trackW, barH), uv, CrimsonInk * (alpha * 0.55f));

            //填充
            int fillW = (int)(trackW * MathHelper.Clamp(progress, 0f, 1f));
            if (fillW > 0) {
                sb.Draw(px, new Rectangle(barRect.X, y, fillW, barH), uv, BladeRed * (alpha * 0.95f));
                //尖端炽亮血光
                if (fillW > 1) {
                    sb.Draw(px, new Rectangle(barRect.X + fillW - 1, y - 1, 1, barH + 2), uv,
                        BladeRedBright * (alpha * 0.85f));
                }
            }

            //满级条带血色脉动
            if (progress >= 0.999f) {
                float fp = MathF.Sin(pulse * 4f) * 0.5f + 0.5f;
                sb.Draw(px, new Rectangle(barRect.X, y, trackW, barH), uv,
                    BladeRedBright * (alpha * 0.18f * fp));
            }

            //不画刻度，锐利刀痕
            if (!string.IsNullOrEmpty(progressText)) {
                var font = FontAssets.MouseText.Value;
                Vector2 sz = font.MeasureString(progressText) * 0.5f;
                Utils.DrawBorderString(sb, progressText,
                    new Vector2(barRect.Right - sz.X - 1f, y - sz.Y - 1f),
                    BladeRedBright * alpha, 0.5f);
            }
        }

        public void DrawWidgetDivider(SpriteBatch sb, Vector2 start, Vector2 end, float alpha) {
            var px = VaultAsset.placeholder2.Value;
            float len = (end - start).Length();
            if (len < 1f) return;
            Vector2 dir = (end - start) / len;
            float rot = MathF.Atan2(dir.Y, dir.X);

            //血迹式不均匀虚线
            for (float c = 0; c < len; c += 6f) {
                float t = c / len;
                float segLen = Math.Min(3f, len - c);
                float fade = MathF.Sin(t * MathHelper.Pi) * 0.55f;
                sb.Draw(px, start + dir * c, new Rectangle(0, 0, 1, 1),
                    BladeRedDim * (alpha * fade), rot, new Vector2(0, 0.5f),
                    new Vector2(segLen, 1f), SpriteEffects.None, 0f);
            }
        }

        public void DrawWidgetOverlay(SpriteBatch sb, Rectangle rect, float alpha) { }

        public Color GetWidgetTitleColor(float alpha) => TitleBlade * alpha;
        public Color GetWidgetTextColor(float alpha) => TextBlade * (alpha * 0.95f);
        public Color GetWidgetAccentColor(float alpha) => BladeRed * alpha;

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

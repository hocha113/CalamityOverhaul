using CalamityOverhaul.Common;
using CalamityOverhaul.Content.UIs.NotificationPopup;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.Localization;

namespace CalamityOverhaul.Content.HackTimes
{
    /// <summary>
    /// 玩家在不满足条件时尝试切换骇客时间触发的警告弹窗
    /// <br/>红色警示系赛博风格，与<see cref="NotificationPopupSystem"/>共用滑入滑出动画与堆叠队列
    /// </summary>
    internal class HackTimeAccessDeniedEntry : NotificationEntry
    {
        private readonly LocalizedText titleOverride;
        private readonly LocalizedText descOverride;

        public HackTimeAccessDeniedEntry() { }

        /// <summary>使用自定义标题/描述复用该红色警示弹窗（例如普通义体界面提醒前往义体医生）</summary>
        public HackTimeAccessDeniedEntry(LocalizedText title, LocalizedText desc) {
            titleOverride = title;
            descOverride = desc;
        }

        public override float Width => 320f;
        public override float Height => 70f;
        public override int SlideTime => 22;
        public override int DisplayTime => 200;
        public override float Gap => 6f;

        public override SoundStyle? AppearSound => CWRSound.FailureCurrent with { Volume = 0.55f, Pitch = -0.1f };

        private static readonly Color PrimaryColor = new(255, 80, 70);
        private static readonly Color AccentColor = new(255, 150, 110);
        private static readonly Color DimBgColor = new(20, 6, 8);

        public override bool OnClick() {
            SoundEngine.PlaySound(CWRSound.ButtonZero with { Volume = 0.55f, Pitch = -0.25f });
            return true;
        }

        public override void DrawContent(SpriteBatch sb, Rectangle r, float alpha) {
            Texture2D px = TextureAssets.MagicPixel.Value;
            float animTime = LifeTimer / 60f;
            float pulse = MathF.Sin(animTime * 4.5f) * 0.5f + 0.5f;

            //外阴影
            for (int d = 4; d >= 1; d--) {
                Rectangle shadow = r;
                shadow.Inflate(d, d);
                shadow.Offset(2, 2);
                sb.Draw(px, shadow, Color.Black * (alpha * 0.05f * d));
            }

            //深色渐变背景
            DrawGradientBg(sb, px, r, alpha);

            //左侧脉冲强调条
            const int barW = 4;
            float barPulse = 0.7f + pulse * 0.3f;
            sb.Draw(px, new Rectangle(r.X, r.Y, barW, r.Height), PrimaryColor * (alpha * barPulse));
            sb.Draw(px, new Rectangle(r.X + barW, r.Y + 1, 12, r.Height - 2),
                PrimaryColor * (alpha * 0.10f * (0.6f + pulse * 0.4f)));

            //四边框系统
            sb.Draw(px, new Rectangle(r.X, r.Y, r.Width, 2), PrimaryColor * (alpha * 0.85f));
            sb.Draw(px, new Rectangle(r.X, r.Bottom - 1, r.Width, 1), PrimaryColor * (alpha * 0.35f));
            sb.Draw(px, new Rectangle(r.Right - 1, r.Y, 1, r.Height), PrimaryColor * (alpha * 0.18f));

            //右上角警告斜切
            DrawCornerCut(sb, px, r, alpha);

            //垂直扫描线，强化"系统拒绝"氛围
            DrawScanLine(sb, px, r, barW, alpha, animTime);

            //左侧警告三角图标
            int iconCX = r.X + barW + 22;
            int iconCY = r.Y + r.Height / 2;
            DrawWarningIcon(sb, px, iconCX, iconCY, alpha, pulse);

            //分隔竖线
            int sepX = r.X + barW + 44;
            sb.Draw(px, new Rectangle(sepX, r.Y + 10, 1, r.Height - 20), PrimaryColor * (alpha * 0.22f));

            //标题与描述
            float textX = sepX + 10;
            string title = (titleOverride ?? HackTime.AccessDeniedTitle)?.Value ?? "ACCESS DENIED";
            string desc = (descOverride ?? HackTime.AccessDeniedDesc)?.Value ?? "";

            //标题：渐入 + 微脉冲色
            float titleAlpha = MathHelper.Clamp((LifeTimer - 4) / 10f, 0f, 1f);
            Color titleColor = Color.Lerp(PrimaryColor, AccentColor, 0.3f + pulse * 0.25f);
            Utils.DrawBorderString(sb, title,
                new Vector2(textX, r.Y + 10),
                titleColor * (alpha * titleAlpha), 0.78f);

            //标题下方装饰横线
            DrawGradientHLine(sb, px, (int)textX, r.Y + 28, r.Right - (int)textX - 12,
                PrimaryColor * (alpha * 0.32f), Color.Transparent);

            //描述文本：自动换行裁剪
            float descAlpha = MathHelper.Clamp((LifeTimer - 8) / 12f, 0f, 1f);
            DrawWrappedText(sb, desc,
                new Vector2(textX, r.Y + 32),
                r.Right - textX - 12f, 0.72f,
                Color.White * (alpha * descAlpha * 0.92f));

            //右上角小型数据指示灯
            DrawDataDots(sb, px, r, alpha, animTime);

            //底部进度倒计时条，提示弹窗剩余时间
            DrawCountdownBar(sb, px, r, alpha);
        }

        #region 子绘制

        private static void DrawGradientBg(SpriteBatch sb, Texture2D px, Rectangle r, float alpha) {
            const int segs = 10;
            Color lightBg = Color.Lerp(new Color(28, 8, 10), PrimaryColor, 0.05f);
            for (int i = 0; i < segs; i++) {
                float t = i / (float)segs;
                float t2 = (i + 1) / (float)segs;
                int x1 = r.X + (int)(t * r.Width);
                int x2 = r.X + (int)(t2 * r.Width);
                sb.Draw(px, new Rectangle(x1, r.Y, Math.Max(1, x2 - x1), r.Height),
                    Color.Lerp(DimBgColor, lightBg, t) * (alpha * 0.9f));
            }
        }

        private static void DrawCornerCut(SpriteBatch sb, Texture2D px, Rectangle r, float alpha) {
            const int cut = 12;
            for (int i = 0; i < cut; i++) {
                int h = Math.Max(cut - i, 1);
                sb.Draw(px, new Rectangle(r.Right - cut + i, r.Y, 1, h),
                    Color.Black * (alpha * 0.65f));
            }
            for (int i = 0; i < cut; i++) {
                sb.Draw(px, new Rectangle(r.Right - cut + i, r.Y + (cut - i - 1), 1, 1),
                    PrimaryColor * (alpha * 0.55f));
            }
        }

        private static void DrawScanLine(SpriteBatch sb, Texture2D px, Rectangle r,
            int barW, float alpha, float animTime) {
            const float period = 1.5f;
            float scanNorm = ((animTime * 1.1f) % period - 0.15f) / period;
            int scanY = r.Y + (int)(scanNorm * r.Height);
            if (scanY >= r.Y + 2 && scanY < r.Bottom - 3) {
                float fade = 1f - MathF.Abs(scanNorm - 0.5f) * 2f;
                int lineW = r.Width - barW - 2;
                sb.Draw(px, new Rectangle(r.X + barW + 1, scanY, lineW, 1),
                    PrimaryColor * (alpha * 0.22f * fade));
                sb.Draw(px, new Rectangle(r.X + barW + 1, scanY + 1, lineW, 1),
                    PrimaryColor * (alpha * 0.10f * fade));
            }
        }

        /// <summary>红色警告三角，中间一根感叹号</summary>
        private static void DrawWarningIcon(SpriteBatch sb, Texture2D px,
            int cx, int cy, float alpha, float pulse) {
            int radius = 11;
            Color body = Color.Lerp(PrimaryColor, Color.White, 0.05f) * (alpha * (0.85f + pulse * 0.15f));
            Color edge = Color.Lerp(PrimaryColor, Color.Black, 0.35f) * alpha;

            //三角实心，由顶向下逐行铺
            for (int row = 0; row < radius * 2; row++) {
                int rowWidth = (int)((row / (float)(radius * 2)) * radius * 2);
                int y = cy - radius + row;
                int x = cx - rowWidth / 2;
                sb.Draw(px, new Rectangle(x, y, Math.Max(rowWidth, 1), 1), body);
            }

            //三角下底
            sb.Draw(px, new Rectangle(cx - radius, cy + radius - 1, radius * 2, 1), edge);

            //三角描边（左右斜边模拟）
            for (int row = 0; row < radius * 2; row++) {
                int rowWidth = (int)((row / (float)(radius * 2)) * radius * 2);
                int y = cy - radius + row;
                int leftX = cx - rowWidth / 2;
                int rightX = cx + rowWidth / 2;
                sb.Draw(px, new Rectangle(leftX, y, 1, 1), edge);
                sb.Draw(px, new Rectangle(rightX - 1, y, 1, 1), edge);
            }

            //中央感叹号
            Color exclColor = Color.Black * alpha;
            sb.Draw(px, new Rectangle(cx - 1, cy - radius / 2, 2, radius), exclColor);
            sb.Draw(px, new Rectangle(cx - 1, cy + radius / 2 + 1, 2, 2), exclColor);
        }

        private static void DrawDataDots(SpriteBatch sb, Texture2D px, Rectangle r,
            float alpha, float animTime) {
            const int dotCount = 3;
            const int dotSize = 2;
            const int spacing = 4;
            int startX = r.Right - dotCount * (dotSize + spacing) - 8;
            int dotY = r.Y + 6;
            for (int i = 0; i < dotCount; i++) {
                float phase = (animTime * 5f + i * 0.7f) % MathHelper.TwoPi;
                float brightness = MathF.Sin(phase) * 0.5f + 0.5f;
                sb.Draw(px,
                    new Rectangle(startX + i * (dotSize + spacing), dotY, dotSize, dotSize),
                    PrimaryColor * (alpha * brightness * 0.6f));
            }
        }

        private void DrawCountdownBar(SpriteBatch sb, Texture2D px, Rectangle r, float alpha) {
            int total = SlideTime * 2 + DisplayTime;
            float lifeRatio = MathHelper.Clamp(LifeTimer / (float)total, 0f, 1f);
            float remaining = 1f - lifeRatio;

            int barY = r.Bottom - 4;
            int barMaxW = r.Width - 16;
            int barW = (int)(barMaxW * remaining);

            sb.Draw(px, new Rectangle(r.X + 8, barY, barMaxW, 2), Color.Black * (alpha * 0.4f));
            if (barW > 0) {
                sb.Draw(px, new Rectangle(r.X + 8, barY, barW, 2), PrimaryColor * (alpha * 0.7f));
                sb.Draw(px, new Rectangle(r.X + 8 + barW - 2, barY, 2, 2), Color.White * (alpha * 0.7f));
            }
        }

        #endregion

        #region 工具

        private static void DrawGradientHLine(SpriteBatch sb, Texture2D px,
            int x, int y, int w, Color start, Color end, int segments = 12) {
            if (w <= 0) {
                return;
            }
            for (int i = 0; i < segments; i++) {
                float t = i / (float)segments;
                float t2 = (i + 1) / (float)segments;
                int x1 = x + (int)(t * w);
                int x2 = x + (int)(t2 * w);
                sb.Draw(px, new Rectangle(x1, y, Math.Max(1, x2 - x1), 1),
                    Color.Lerp(start, end, t));
            }
        }

        /// <summary>
        /// 描述文本自动换行绘制，按词/字裁剪以适应面板宽度
        /// </summary>
        private static void DrawWrappedText(SpriteBatch sb, string text,
            Vector2 startPos, float maxWidth, float scale, Color color) {
            if (string.IsNullOrEmpty(text) || maxWidth <= 0f) {
                return;
            }

            var font = FontAssets.MouseText.Value;
            float lineHeight = font.LineSpacing * scale * 0.95f;
            //简单按字符贪心换行，兼容中英混排
            int start = 0;
            float y = startPos.Y;
            int lineIndex = 0;
            const int maxLines = 2;

            while (start < text.Length && lineIndex < maxLines) {
                int end = start;
                int lastFit = start;
                while (end <= text.Length) {
                    string slice = text.Substring(start, end - start);
                    float w = font.MeasureString(slice).X * scale;
                    if (w > maxWidth) {
                        break;
                    }
                    lastFit = end;
                    end++;
                }
                if (lastFit == start) {
                    //单字符已超宽，强制吃掉一个字符避免死循环
                    lastFit = Math.Min(text.Length, start + 1);
                }

                bool isLastLine = lineIndex == maxLines - 1;
                string drawn = text[start..lastFit];
                if (isLastLine && lastFit < text.Length) {
                    //最后一行不够放下时附加省略号
                    const string ellipsis = "...";
                    if (font.MeasureString(ellipsis).X * scale < maxWidth) {
                        for (int len = drawn.Length; len > 0; len--) {
                            string candidate = drawn[..len] + ellipsis;
                            if (font.MeasureString(candidate).X * scale <= maxWidth) {
                                drawn = candidate;
                                break;
                            }
                        }
                    }
                }
                Utils.DrawBorderString(sb, drawn, new Vector2(startPos.X, y), color, scale);
                y += lineHeight;
                start = lastFit;
                lineIndex++;
            }
        }

        #endregion
    }
}

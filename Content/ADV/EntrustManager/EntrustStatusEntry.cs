using CalamityOverhaul.Content.UIs.NotificationPopup;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;

namespace CalamityOverhaul.Content.ADV.EntrustManager
{
    /// <summary>委托状态变更通知弹窗</summary>
    internal class EntrustStatusEntry : NotificationEntry
    {
        public enum StatusKind
        {
            NewQuest,
            Tracked,
            Untracked,
            Suspended,
            Unsuspended,
            Completed,
        }

        private readonly string questTitle;
        private readonly StatusKind kind;

        public override float Width => 280f;
        public override float Height => 56f;
        public override int SlideTime => 22;
        public override int DisplayTime => 160;
        public override float Gap => 5f;

        public EntrustStatusEntry(string questTitle, StatusKind kind) {
            this.questTitle = questTitle;
            this.kind = kind;
        }

        #region 状态色板

        private Color GetPrimaryColor() => kind switch {
            StatusKind.NewQuest => new Color(80, 200, 255),
            StatusKind.Tracked => new Color(80, 220, 170),
            StatusKind.Untracked => new Color(130, 140, 155),
            StatusKind.Suspended => new Color(220, 180, 80),
            StatusKind.Unsuspended => new Color(120, 220, 140),
            StatusKind.Completed => new Color(255, 210, 80),
            _ => Color.White
        };

        private Color GetAccentColor() => kind switch {
            StatusKind.NewQuest => new Color(100, 255, 240),
            StatusKind.Tracked => new Color(100, 255, 180),
            StatusKind.Untracked => new Color(100, 110, 125),
            StatusKind.Suspended => new Color(255, 210, 100),
            StatusKind.Unsuspended => new Color(140, 255, 160),
            StatusKind.Completed => new Color(255, 230, 140),
            _ => Color.White
        };

        private Color GetLabelColor() => kind switch {
            StatusKind.NewQuest => new Color(100, 230, 255),
            StatusKind.Tracked => new Color(100, 255, 180),
            StatusKind.Untracked => new Color(155, 160, 170),
            StatusKind.Suspended => new Color(235, 195, 85),
            StatusKind.Unsuspended => new Color(140, 235, 150),
            StatusKind.Completed => Color.Gold,
            _ => Color.White
        };

        private bool IsImportant => kind is StatusKind.NewQuest or StatusKind.Completed;

        #endregion

        public override void DrawContent(SpriteBatch sb, Rectangle r, float alpha) {
            Texture2D px = VaultAsset.placeholder2.Value;
            Color primary = GetPrimaryColor();
            Color accent = GetAccentColor();
            float animTime = LifeTimer / 60f;
            float pulse = MathF.Sin(animTime * 3.5f) * 0.5f + 0.5f;

            //阴影
            DrawSoftShadow(sb, px, r, alpha);

            //渐变背景
            DrawGradientBg(sb, px, r, primary, alpha);

            //点阵底纹，Untracked 跳过
            if (kind != StatusKind.Untracked)
                DrawGridPattern(sb, px, r, primary, alpha);

            //左侧强调条
            const int barW = 4;
            float barPulse = IsImportant ? 0.7f + pulse * 0.3f : 0.85f + pulse * 0.15f;
            sb.Draw(px, new Rectangle(r.X, r.Y, barW, r.Height), primary * (alpha * barPulse));
            sb.Draw(px, new Rectangle(r.X + barW, r.Y + 1, 10, r.Height - 2),
                primary * (alpha * 0.08f * (0.6f + pulse * 0.4f)));
            sb.Draw(px, new Rectangle(r.X + barW, r.Y + 1, 5, r.Height - 2),
                primary * (alpha * 0.05f));

            //边框
            //顶边双线
            sb.Draw(px, new Rectangle(r.X, r.Y, r.Width, 2), primary * (alpha * 0.85f));
            sb.Draw(px, new Rectangle(r.X + barW, r.Y + 3, r.Width - barW - 12, 1),
                primary * (alpha * 0.18f));
            //底边渐变
            DrawGradientHLine(sb, px, r.X, r.Bottom - 1, r.Width,
                primary * (alpha * 0.5f), Color.Transparent);
            DrawGradientHLine(sb, px, r.X, r.Bottom - 2, (int)(r.Width * 0.6f),
                primary * (alpha * 0.15f), Color.Transparent);
            //右侧细线
            sb.Draw(px, new Rectangle(r.Right - 1, r.Y, 1, r.Height),
                primary * (alpha * 0.2f));

            //右上斜切
            DrawCornerCut(sb, px, r, primary, alpha);

            //扫描线
            DrawScanLine(sb, px, r, barW, primary, alpha, animTime);

            //状态图标
            int iconCX = r.X + barW + 20;
            int iconCY = r.Y + r.Height / 2;
            DrawStatusIcon(sb, px, iconCX, iconCY, primary, accent, alpha, pulse);

            //分隔竖线
            int sepX = r.X + barW + 38;
            sb.Draw(px, new Rectangle(sepX, r.Y + 8, 1, r.Height - 16),
                primary * (alpha * 0.2f));

            //状态标签
            float textX = sepX + 8;
            string label = GetLabel();
            Color labelC = GetLabelColor();
            float labelGlow = IsImportant ? pulse * 0.12f : 0f;
            Utils.DrawBorderString(sb, label,
                new Vector2(textX, r.Y + 8),
                labelC * MathHelper.Clamp(alpha + labelGlow, 0f, 1f), 0.68f);

            //标签下划线
            int labelW = (int)(FontAssets.MouseText.Value.MeasureString(label).X * 0.68f);
            int lineMaxW = r.Right - (int)textX - 10;
            DrawGradientHLine(sb, px, (int)textX, r.Y + 23,
                Math.Min(labelW + 10, lineMaxW),
                primary * (alpha * 0.25f), Color.Transparent);

            //委托名称
            string displayTitle = TruncateTextToWidth(questTitle, r.Width - (textX - r.X) - 16f, 0.72f);
            Utils.DrawBorderString(sb, displayTitle,
                new Vector2(textX, r.Y + 28),
                Color.White * (alpha * 0.92f), 0.72f);

            //数据灯
            DrawDataTicker(sb, px, r, primary, alpha, animTime);

            //状态叠加
            DrawStatusOverlay(sb, px, r, barW, primary, accent, alpha, pulse, animTime);
        }

        #region 背景绘制

        /// <summary>扩散柔和阴影</summary>
        private static void DrawSoftShadow(SpriteBatch sb, Texture2D px, Rectangle r, float alpha) {
            for (int d = 4; d >= 1; d--) {
                Rectangle shadow = r;
                shadow.Inflate(d, d);
                shadow.Offset(2, 2);
                sb.Draw(px, shadow, Color.Black * (alpha * 0.04f * d));
            }
        }

        /// <summary>左暗右亮渐变底色</summary>
        private static void DrawGradientBg(SpriteBatch sb, Texture2D px, Rectangle r,
            Color primary, float alpha) {
            const int segs = 10;
            Color deepBg = new(5, 8, 18);
            Color lightBg = Color.Lerp(new Color(10, 16, 32), primary, 0.06f);
            for (int i = 0; i < segs; i++) {
                float t = i / (float)segs;
                float t2 = (i + 1) / (float)segs;
                int x1 = r.X + (int)(t * r.Width);
                int x2 = r.X + (int)(t2 * r.Width);
                sb.Draw(px, new Rectangle(x1, r.Y, Math.Max(1, x2 - x1), r.Height),
                    Color.Lerp(deepBg, lightBg, t) * (alpha * 0.88f));
            }
        }

        /// <summary>微弱点阵底纹，增加质感</summary>
        private static void DrawGridPattern(SpriteBatch sb, Texture2D px, Rectangle r,
            Color primary, float alpha) {
            const int spacing = 12;
            const float baseOp = 0.04f;
            for (int gy = r.Y + 6; gy < r.Bottom - 4; gy += spacing) {
                for (int gx = r.X + 8; gx < r.Right - 4; gx += spacing) {
                    float dist = (gx - r.X) / (float)r.Width;
                    float op = baseOp * (0.5f + dist * 0.5f);
                    sb.Draw(px, new Rectangle(gx, gy, 1, 1), primary * (alpha * op));
                }
            }
        }

        #endregion

        #region 边框装饰

        /// <summary>右上角三角斜切 + 对角线</summary>
        private static void DrawCornerCut(SpriteBatch sb, Texture2D px, Rectangle r,
            Color primary, float alpha) {
            const int cut = 10;
            for (int i = 0; i < cut; i++) {
                int h = Math.Max(cut - i, 1);
                sb.Draw(px, new Rectangle(r.Right - cut + i, r.Y, 1, h),
                    Color.Black * (alpha * 0.6f));
            }
            for (int i = 0; i < cut; i++) {
                sb.Draw(px, new Rectangle(r.Right - cut + i, r.Y + (cut - i - 1), 1, 1),
                    primary * (alpha * 0.5f));
            }
        }

        #endregion

        #region 扫描线

        /// <summary>垂直扫描光带</summary>
        private void DrawScanLine(SpriteBatch sb, Texture2D px, Rectangle r,
            int barW, Color primary, float alpha, float animTime) {
            if (kind is not (StatusKind.NewQuest or StatusKind.Completed or StatusKind.Unsuspended))
                return;

            float speed = kind == StatusKind.Completed ? 0.7f : 1.0f;
            const float period = 1.3f;
            float scanNorm = ((animTime * speed) % period - 0.15f) / period;
            int scanY = r.Y + (int)(scanNorm * r.Height);

            if (scanY >= r.Y + 2 && scanY < r.Bottom - 3) {
                float fade = 1f - MathF.Abs(scanNorm - 0.5f) * 2f;
                int lineW = r.Width - barW - 2;
                sb.Draw(px, new Rectangle(r.X + barW + 1, scanY, lineW, 1),
                    primary * (alpha * 0.25f * fade));
                sb.Draw(px, new Rectangle(r.X + barW + 1, scanY + 1, lineW, 1),
                    primary * (alpha * 0.10f * fade));
            }
        }

        #endregion

        #region 状态图标

        private void DrawStatusIcon(SpriteBatch sb, Texture2D px, int cx, int cy,
            Color primary, Color accent, float alpha, float pulse) {
            switch (kind) {
                case StatusKind.NewQuest:
                    DrawDiamond(sb, px, cx, cy, 7, accent * alpha, pulse);
                    break;
                case StatusKind.Tracked:
                    DrawCrosshair(sb, px, cx, cy, 7, primary * alpha);
                    break;
                case StatusKind.Untracked:
                    DrawXMark(sb, px, cx, cy, 5, primary * (alpha * 0.65f));
                    break;
                case StatusKind.Suspended:
                    DrawPauseBars(sb, px, cx, cy, 6, primary * alpha, pulse);
                    break;
                case StatusKind.Unsuspended:
                    DrawPlayArrow(sb, px, cx, cy, 6, primary * alpha, pulse);
                    break;
                case StatusKind.Completed:
                    DrawCheckmark(sb, px, cx, cy, 7, accent * alpha, pulse);
                    break;
            }
        }

        /// <summary>脉冲菱形，新委托</summary>
        private static void DrawDiamond(SpriteBatch sb, Texture2D px,
            int cx, int cy, int radius, Color c, float pulse) {
            int r = (int)(radius * (0.85f + pulse * 0.15f));
            for (int i = 0; i <= r; i++) {
                sb.Draw(px, new Rectangle(cx - i, cy - r + i, 1, 1), c);
                sb.Draw(px, new Rectangle(cx + i, cy - r + i, 1, 1), c);
                sb.Draw(px, new Rectangle(cx - i, cy + r - i, 1, 1), c);
                sb.Draw(px, new Rectangle(cx + i, cy + r - i, 1, 1), c);
            }
            sb.Draw(px, new Rectangle(cx - 1, cy - 1, 3, 3), c * (0.4f + pulse * 0.3f));
        }

        /// <summary>十字准星，已关注</summary>
        private static void DrawCrosshair(SpriteBatch sb, Texture2D px,
            int cx, int cy, int radius, Color c) {
            sb.Draw(px, new Rectangle(cx - radius, cy, radius * 2 + 1, 1), c);
            sb.Draw(px, new Rectangle(cx, cy - radius, 1, radius * 2 + 1), c);
            const int bLen = 3;
            Color dim = c * 0.6f;
            //四角括号
            sb.Draw(px, new Rectangle(cx - radius, cy - radius, bLen, 1), dim);
            sb.Draw(px, new Rectangle(cx - radius, cy - radius, 1, bLen), dim);
            sb.Draw(px, new Rectangle(cx + radius - bLen + 1, cy - radius, bLen, 1), dim);
            sb.Draw(px, new Rectangle(cx + radius, cy - radius, 1, bLen), dim);
            sb.Draw(px, new Rectangle(cx - radius, cy + radius, bLen, 1), dim);
            sb.Draw(px, new Rectangle(cx - radius, cy + radius - bLen + 1, 1, bLen), dim);
            sb.Draw(px, new Rectangle(cx + radius - bLen + 1, cy + radius, bLen, 1), dim);
            sb.Draw(px, new Rectangle(cx + radius, cy + radius - bLen + 1, 1, bLen), dim);
            sb.Draw(px, new Rectangle(cx, cy, 1, 1), c);
        }

        /// <summary>X 交叉，取消关注</summary>
        private static void DrawXMark(SpriteBatch sb, Texture2D px,
            int cx, int cy, int radius, Color c) {
            for (int i = -radius; i <= radius; i++) {
                sb.Draw(px, new Rectangle(cx + i, cy + i, 1, 1), c);
                sb.Draw(px, new Rectangle(cx + i, cy - i, 1, 1), c);
            }
        }

        /// <summary>暂停双竖条</summary>
        private static void DrawPauseBars(SpriteBatch sb, Texture2D px,
            int cx, int cy, int halfH, Color c, float pulse) {
            const int barW = 2;
            const int gap = 3;
            float breathe = 0.75f + pulse * 0.25f;
            Color bc = c * breathe;
            sb.Draw(px, new Rectangle(cx - gap - barW + 1, cy - halfH, barW, halfH * 2), bc);
            sb.Draw(px, new Rectangle(cx + gap, cy - halfH, barW, halfH * 2), bc);
        }

        /// <summary>播放三角，恢复态</summary>
        private static void DrawPlayArrow(SpriteBatch sb, Texture2D px,
            int cx, int cy, int halfH, Color c, float pulse) {
            Color gc = c * (0.8f + pulse * 0.2f);
            for (int row = -halfH; row <= halfH; row++) {
                int w = halfH - Math.Abs(row);
                if (w > 0)
                    sb.Draw(px, new Rectangle(cx - halfH / 2, cy + row, w, 1), gc);
            }
        }

        /// <summary>对勾，完成</summary>
        private static void DrawCheckmark(SpriteBatch sb, Texture2D px,
            int cx, int cy, int size, Color c, float pulse) {
            Color gc = c * (0.85f + pulse * 0.15f);
            int halfS = size / 2;
            //短腿（左下→中间）
            for (int i = 0; i < halfS; i++)
                sb.Draw(px, new Rectangle(cx - halfS + i, cy + i, 2, 1), gc);
            //长腿（中间→右上）
            for (int i = 0; i < size; i++)
                sb.Draw(px, new Rectangle(cx - halfS + halfS + i, cy + halfS - 1 - i, 2, 1), gc);
            //顶点辉光
            sb.Draw(px, new Rectangle(cx - 1, cy + halfS - 2, 3, 3), c * (0.3f * pulse));
        }

        #endregion

        #region 数据灯 & 叠加特效

        /// <summary>右上角三个脉冲数据指示灯</summary>
        private void DrawDataTicker(SpriteBatch sb, Texture2D px, Rectangle r,
            Color primary, float alpha, float animTime) {
            const int dotCount = 3;
            const int dotSize = 2;
            const int spacing = 4;
            int startX = r.Right - dotCount * (dotSize + spacing) - 6;
            int dotY = r.Y + 6;
            for (int i = 0; i < dotCount; i++) {
                float phase = (animTime * 4f + i * 0.8f) % MathHelper.TwoPi;
                float brightness = MathF.Sin(phase) * 0.5f + 0.5f;
                sb.Draw(px,
                    new Rectangle(startX + i * (dotSize + spacing), dotY, dotSize, dotSize),
                    primary * (alpha * brightness * 0.5f));
            }
        }

        /// <summary>每种状态独有的叠加视觉特效</summary>
        private void DrawStatusOverlay(SpriteBatch sb, Texture2D px, Rectangle r,
            int barW, Color primary, Color accent, float alpha, float pulse, float animTime) {
            int innerX = r.X + barW + 1;
            int innerW = r.Width - barW - 2;

            switch (kind) {
                case StatusKind.NewQuest: {
                    //向下扩散的水平波纹
                    float ripple = (animTime * 1.2f) % 1.2f;
                    int rippleY = r.Y + (int)(ripple * r.Height);
                    if (rippleY > r.Y + 1 && rippleY < r.Bottom - 2) {
                        float op = (1f - ripple / 1.2f) * 0.15f;
                        sb.Draw(px, new Rectangle(innerX, rippleY, innerW, 1),
                            accent * (alpha * op));
                    }
                    break;
                }
                case StatusKind.Completed: {
                    //对角线金色流光扫过
                    float sweep = (animTime * 0.5f) % 2.5f;
                    if (sweep < 1f) {
                        int sweepX = r.X + (int)(sweep * (r.Width + 30)) - 15;
                        for (int i = 0; i < 20; i++) {
                            int sx = sweepX + i;
                            if (sx > innerX && sx < r.Right - 1) {
                                float intense = MathF.Sin(i / 20f * MathF.PI) * 0.12f;
                                sb.Draw(px, new Rectangle(sx, r.Y + 2, 1, r.Height - 4),
                                    accent * (alpha * intense));
                            }
                        }
                    }
                    break;
                }
                case StatusKind.Suspended: {
                    //缓慢琥珀色呼吸叠加
                    float breathe = MathF.Sin(animTime * 1.5f) * 0.025f;
                    if (breathe > 0f)
                        sb.Draw(px, new Rectangle(innerX, r.Y + 2, innerW, r.Height - 4),
                            primary * (alpha * breathe));
                    break;
                }
                case StatusKind.Untracked: {
                    //水平暗纹
                    for (int sy = r.Y + 4; sy < r.Bottom - 2; sy += 6)
                        sb.Draw(px, new Rectangle(innerX, sy, innerW, 1),
                            Color.Black * (alpha * 0.08f));
                    break;
                }
            }
        }

        #endregion

        #region 工具方法

        private string GetLabel() => kind switch {
            StatusKind.NewQuest => EntrustManagerNotification.LabelNewQuest.Value,
            StatusKind.Tracked => EntrustManagerNotification.LabelTracked.Value,
            StatusKind.Untracked => EntrustManagerNotification.LabelUntracked.Value,
            StatusKind.Suspended => EntrustManagerNotification.LabelSuspended.Value,
            StatusKind.Unsuspended => EntrustManagerNotification.LabelUnsuspended.Value,
            StatusKind.Completed => EntrustManagerNotification.LabelCompleted.Value,
            _ => ""
        };

        private static void DrawGradientHLine(SpriteBatch sb, Texture2D px,
            int x, int y, int w, Color start, Color end, int segments = 12) {
            for (int i = 0; i < segments; i++) {
                float t = i / (float)segments;
                float t2 = (i + 1) / (float)segments;
                int x1 = x + (int)(t * w);
                int x2 = x + (int)(t2 * w);
                sb.Draw(px, new Rectangle(x1, y, Math.Max(1, x2 - x1), 1),
                    Color.Lerp(start, end, t));
            }
        }

        private static string TruncateTextToWidth(string text, float maxWidth, float scale) {
            if (string.IsNullOrEmpty(text) || maxWidth <= 0f)
                return string.Empty;

            var font = FontAssets.MouseText.Value;
            if (font.MeasureString(text).X * scale <= maxWidth)
                return text;

            const string ellipsis = "...";
            float ellipsisWidth = font.MeasureString(ellipsis).X * scale;
            if (ellipsisWidth >= maxWidth)
                return ellipsis;

            for (int length = text.Length - 1; length > 0; length--) {
                string candidate = text[..length] + ellipsis;
                if (font.MeasureString(candidate).X * scale <= maxWidth)
                    return candidate;
            }

            return ellipsis;
        }

        #endregion
    }
}

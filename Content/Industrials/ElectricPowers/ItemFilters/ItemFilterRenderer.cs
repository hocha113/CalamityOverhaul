using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using System;
using Terraria;
using Terraria.GameContent;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.ItemFilters
{
    /// <summary>
    /// 过滤名单编辑器的绘制层：全部为无状态静态方法，
    /// 1像素白纹理程序化绘制，配色取自 <see cref="ItemFilterTheme"/>
    /// </summary>
    internal static class ItemFilterRenderer
    {
        private static Texture2D Px => VaultAsset.placeholder2.Value;
        private static Rectangle Src => new(0, 0, 1, 1);
        private static DynamicSpriteFont Font => FontAssets.MouseText.Value;

        /// <summary>面板外壳：投影 + 锈色渐变底 + 扫描线 + 边框与四角饰</summary>
        public static void DrawChrome(SpriteBatch sb, Rectangle rect, float alpha, float time) {
            //柔和投影
            for (int d = 7; d >= 1; d--) {
                Rectangle shadow = rect;
                shadow.Inflate(d, d);
                shadow.Offset(4, 5);
                sb.Draw(Px, shadow, Src, Color.Black * (alpha * 0.05f * (8 - d)));
            }

            //纵向渐变底，暗锈脉动
            const int segments = 36;
            for (int i = 0; i < segments; i++) {
                float t = i / (float)segments;
                int y1 = rect.Y + (int)(t * rect.Height);
                int y2 = rect.Y + (int)((i + 1) / (float)segments * rect.Height);
                float pulse = MathF.Sin(time * 0.8f + t * 2.5f) * 0.5f + 0.5f;
                Color baseColor = Color.Lerp(ItemFilterTheme.Void, ItemFilterTheme.RustMid, pulse * 0.55f);
                Color finalColor = Color.Lerp(baseColor, ItemFilterTheme.WarmEdge, t * 0.3f) * (alpha * 0.94f);
                sb.Draw(Px, new Rectangle(rect.X, y1, rect.Width, Math.Max(1, y2 - y1)), Src, finalColor);
            }

            //慢速扫描线
            float scanY = rect.Y + (MathF.Sin(time * 0.6f) * 0.5f + 0.5f) * rect.Height;
            for (int i = -2; i <= 2; i++) {
                float offsetY = scanY + i * 3f;
                if (offsetY < rect.Y || offsetY > rect.Bottom) {
                    continue;
                }
                float intensity = 1f - Math.Abs(i) * 0.3f;
                sb.Draw(Px, new Rectangle(rect.X + 10, (int)offsetY, rect.Width - 20, i == 0 ? 2 : 1)
                    , Src, new Color(200, 100, 60) * (alpha * 0.08f * intensity));
            }

            //脉动边框
            float framePulse = MathF.Sin(time * 1.5f) * 0.5f + 0.5f;
            Color edge = Color.Lerp(ItemFilterTheme.EdgeRust, ItemFilterTheme.EdgeBright, framePulse) * (alpha * 0.8f);
            sb.Draw(Px, new Rectangle(rect.X, rect.Y, rect.Width, 3), Src, edge);
            sb.Draw(Px, new Rectangle(rect.X, rect.Bottom - 3, rect.Width, 3), Src, edge * 0.65f);
            sb.Draw(Px, new Rectangle(rect.X, rect.Y, 3, rect.Height), Src, edge * 0.8f);
            sb.Draw(Px, new Rectangle(rect.Right - 3, rect.Y, 3, rect.Height), Src, edge * 0.8f);

            //四角饰角
            const int cornerLen = 14;
            Color corner = ItemFilterTheme.EdgeBright * (alpha * (0.55f + framePulse * 0.35f));
            DrawCorner(sb, new Vector2(rect.X, rect.Y), 1, 1, cornerLen, corner);
            DrawCorner(sb, new Vector2(rect.Right, rect.Y), -1, 1, cornerLen, corner);
            DrawCorner(sb, new Vector2(rect.X, rect.Bottom), 1, -1, cornerLen, corner);
            DrawCorner(sb, new Vector2(rect.Right, rect.Bottom), -1, -1, cornerLen, corner);
        }

        private static void DrawCorner(SpriteBatch sb, Vector2 origin, int dx, int dy, int len, Color color) {
            Vector2 horizontal = dx > 0 ? origin : origin - new Vector2(len, 0);
            Vector2 vertical = dy > 0 ? origin : origin - new Vector2(0, len);
            sb.Draw(Px, new Rectangle((int)horizontal.X, (int)origin.Y - (dy < 0 ? 2 : 0), len, 2), Src, color);
            sb.Draw(Px, new Rectangle((int)origin.X - (dx < 0 ? 2 : 0), (int)vertical.Y, 2, len), Src, color);
        }

        /// <summary>标题下的发光分隔线</summary>
        public static void DrawDivider(SpriteBatch sb, Vector2 start, float width, float alpha, float time) {
            float flow = (time * 30f) % 24f;
            for (float x = -flow; x < width; x += 24f) {
                float segStart = Math.Max(0f, x);
                float segEnd = Math.Min(width, x + 14f);
                if (segEnd <= segStart) {
                    continue;
                }
                sb.Draw(Px, new Rectangle((int)(start.X + segStart), (int)start.Y, (int)(segEnd - segStart), 1)
                    , Src, ItemFilterTheme.EdgeRust * (alpha * 0.7f));
            }
        }

        /// <summary>名单格子：暗色内嵌底 + 锈边 + 物品图标，悬停时亮边微放大</summary>
        public static void DrawCell(SpriteBatch sb, Rectangle rect, int itemType
            , float ease, float hover, float flash, float alpha, Color modeAccent) {
            if (ease <= 0.01f) {
                return;
            }

            //出场缩放
            float scale = 0.6f + 0.4f * ease;
            Rectangle cell = ScaledRect(rect, scale);
            float cellAlpha = alpha * ease;

            Color bg = Color.Lerp(ItemFilterTheme.PanelDark, ItemFilterTheme.WarmEdge, hover * 0.7f);
            sb.Draw(Px, cell, Src, bg * (cellAlpha * 0.92f));

            Color border = Color.Lerp(ItemFilterTheme.EdgeRust * 0.75f, modeAccent, hover) * cellAlpha;
            DrawRectOutline(sb, cell, border, 1);

            //悬停角标
            if (hover > 0.05f) {
                const int len = 7;
                Color bracket = modeAccent * (cellAlpha * hover);
                sb.Draw(Px, new Rectangle(cell.X, cell.Y, len, 2), Src, bracket);
                sb.Draw(Px, new Rectangle(cell.X, cell.Y, 2, len), Src, bracket);
                sb.Draw(Px, new Rectangle(cell.Right - len, cell.Bottom - 2, len, 2), Src, bracket);
                sb.Draw(Px, new Rectangle(cell.Right - 2, cell.Bottom - len, 2, len), Src, bracket);
            }

            //重复添加提示闪光
            if (flash > 0.01f) {
                sb.Draw(Px, cell, Src, ItemFilterTheme.Gold * (cellAlpha * flash * 0.45f));
            }

            VaultUtils.SafeLoadItem(itemType);
            float itemBrightness = 0.82f + 0.18f * hover;
            VaultUtils.SimpleDrawItem(sb, itemType, cell.Center.ToVector2()
                , itemWidth: (int)(32 * scale), (1f + hover * 0.08f) * scale, 0
                , new Color(itemBrightness, itemBrightness, itemBrightness, 1f) * cellAlpha);
        }

        /// <summary>被移除条目的残影：原位淡出收缩</summary>
        public static void DrawGhost(SpriteBatch sb, Rectangle rect, int itemType, float fade, float alpha) {
            if (fade <= 0.01f) {
                return;
            }
            Rectangle cell = ScaledRect(rect, fade);
            Color tint = ItemFilterTheme.Danger * (alpha * fade * 0.6f);
            DrawRectOutline(sb, cell, tint, 1);
            VaultUtils.SafeLoadItem(itemType);
            VaultUtils.SimpleDrawItem(sb, itemType, cell.Center.ToVector2()
                , itemWidth: (int)(32 * fade), fade, 0, Color.White * (alpha * fade * 0.55f));
        }

        /// <summary>底部操作按钮</summary>
        public static void DrawButton(SpriteBatch sb, Rectangle rect, string text, bool hovering, float alpha, Color accent) {
            Color bgColor = hovering ? new Color(50, 30, 20) : new Color(25, 16, 12);
            Color borderColor = hovering ? Color.Lerp(accent, Color.White, 0.3f) : accent * 0.7f;

            sb.Draw(Px, rect, Src, bgColor * (alpha * 0.9f));
            DrawRectOutline(sb, rect, borderColor * (alpha * 0.75f), 1);

            Color textColor = hovering ? new Color(255, 220, 180) : ItemFilterTheme.TextWarm * 0.9f;
            Vector2 textSize = Font.MeasureString(text) * 0.58f;
            Utils.DrawBorderString(sb, text, rect.Center.ToVector2() - textSize / 2f, textColor * alpha, 0.58f);
        }

        /// <summary>黑/白名单模式芯片：LED + 模式名</summary>
        public static void DrawModeChip(SpriteBatch sb, Rectangle rect, string label
            , ItemFilterMode mode, bool hovering, float alpha, float time) {
            Color accent = ItemFilterTheme.ModeAccent(mode);
            Color bg = hovering ? new Color(48, 30, 22) : new Color(24, 15, 12);
            sb.Draw(Px, rect, Src, bg * (alpha * 0.92f));
            DrawRectOutline(sb, rect, accent * (alpha * (hovering ? 0.95f : 0.6f)), 1);

            float pulse = MathF.Sin(time * 2.2f) * 0.3f + 0.7f;
            Vector2 ledPos = new(rect.X + 11, rect.Center.Y);
            sb.Draw(Px, ledPos, Src, accent * (alpha * pulse), 0f, new Vector2(0.5f), 5f, SpriteEffects.None, 0f);
            sb.Draw(Px, ledPos, Src, Color.White * (alpha * 0.35f * pulse), 0f, new Vector2(0.5f), 2.4f, SpriteEffects.None, 0f);

            Vector2 textSize = Font.MeasureString(label) * 0.58f;
            Utils.DrawBorderString(sb, label, new Vector2(rect.X + 20, rect.Center.Y - textSize.Y / 2f)
                , Color.Lerp(ItemFilterTheme.TextWarm, accent, 0.45f) * alpha, 0.58f);
        }

        /// <summary>右侧滚动条</summary>
        public static void DrawScrollbar(SpriteBatch sb, Rectangle track, float progress, float thumbHeightRatio, float alpha) {
            sb.Draw(Px, track, Src, ItemFilterTheme.EdgeRust * (alpha * 0.28f));
            int thumbH = Math.Max(20, (int)(track.Height * thumbHeightRatio));
            int thumbY = track.Y + (int)(progress * (track.Height - thumbH));
            Rectangle thumb = new(track.X - 1, thumbY, track.Width + 2, thumbH);
            sb.Draw(Px, thumb, Src, ItemFilterTheme.EdgeBright * (alpha * 0.85f));
            sb.Draw(Px, new Rectangle(thumb.X, thumb.Y, thumb.Width, 1), Src, ItemFilterTheme.Gold * (alpha * 0.6f));
        }

        /// <summary>空名单占位提示</summary>
        public static void DrawEmptyHint(SpriteBatch sb, Rectangle viewport, string text, float alpha) {
            float pulse = MathF.Sin(Main.GlobalTimeWrappedHourly * 2.2f) * 0.2f + 0.6f;
            Vector2 size = Font.MeasureString(text) * 0.68f;
            Utils.DrawBorderString(sb, text, viewport.Center.ToVector2() - size / 2f
                , ItemFilterTheme.TextDim * (alpha * pulse), 0.68f);
        }

        public static void DrawRectOutline(SpriteBatch sb, Rectangle rect, Color color, int thickness) {
            sb.Draw(Px, new Rectangle(rect.X, rect.Y, rect.Width, thickness), Src, color);
            sb.Draw(Px, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), Src, color * 0.7f);
            sb.Draw(Px, new Rectangle(rect.X, rect.Y, thickness, rect.Height), Src, color * 0.85f);
            sb.Draw(Px, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), Src, color * 0.85f);
        }

        private static Rectangle ScaledRect(Rectangle rect, float scale) {
            int w = (int)(rect.Width * scale);
            int h = (int)(rect.Height * scale);
            return new Rectangle(rect.Center.X - w / 2, rect.Center.Y - h / 2, w, h);
        }
    }
}

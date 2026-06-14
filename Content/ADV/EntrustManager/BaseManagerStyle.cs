using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;

namespace CalamityOverhaul.Content.ADV.EntrustManager
{
    /// <summary>管理器样式基类，通用绘制工具</summary>
    internal abstract class BaseManagerStyle : IEntrustManagerStyle
    {
        protected float pulseTimer;
        protected float globalTimer;

        #region IQuestManagerStyle 默认实现

        public virtual void Update(Rectangle panelRect, float openProgress) {
            pulseTimer += 0.025f;
            if (pulseTimer > MathHelper.TwoPi) pulseTimer -= MathHelper.TwoPi;
            globalTimer += 0.016f;
            if (globalTimer > MathHelper.TwoPi) globalTimer -= MathHelper.TwoPi;
        }

        public virtual void Reset() {
            pulseTimer = 0f;
            globalTimer = 0f;
        }

        public abstract void DrawPanelBackground(SpriteBatch sb, Rectangle panelRect, float alpha);
        public abstract void DrawPanelFrame(SpriteBatch sb, Rectangle panelRect, float alpha);
        public abstract void DrawHeader(SpriteBatch sb, Rectangle headerRect, string title, float alpha);
        public abstract void DrawCategoryTabs(SpriteBatch sb, Rectangle tabRect, string[] categories,
            int selectedIndex, float alpha);
        public abstract void DrawScrollbar(SpriteBatch sb, Rectangle trackRect, float scrollRatio,
            float viewRatio, float alpha);
        public abstract void DrawFooter(SpriteBatch sb, Rectangle footerRect, int totalQuests,
            int activeQuests, float alpha);
        public abstract void DrawQuestEntry(SpriteBatch sb, Rectangle entryRect, EntrustEntryData entry,
            bool isSelected, bool isHovered, float alpha, int entryIndex);
        public abstract void DrawEntrySeparator(SpriteBatch sb, Vector2 start, Vector2 end, float alpha);
        public abstract Color GetShadowColor(float alpha);
        public abstract Color GetHeaderTextColor(float alpha);
        public abstract Color GetStatusColor(QuestEntryStatus status, float alpha);
        public virtual int GetEntryHeight() => 62;
        public virtual int GetEntryPadding() => 4;
        public abstract void DrawParticles(SpriteBatch sb, Rectangle panelRect, float alpha);
        public abstract void DrawOverlayEffects(SpriteBatch sb, Rectangle panelRect, float alpha);
        public abstract Rectangle GetStyleSwitchButtonRect(Rectangle panelRect);
        public abstract void DrawStyleSwitchButton(SpriteBatch sb, Rectangle panelRect, bool isHovered, float alpha);

        #endregion

        #region 通用工具方法

        protected static Texture2D Px => VaultAsset.placeholder2.Value;

        /// <summary>像素水平线</summary>
        protected static void HLine(SpriteBatch sb, int x, int y, int w, Color c) {
            sb.Draw(Px, new Rectangle(x, y, w, 1), new Rectangle(0, 0, 1, 1), c);
        }

        /// <summary>像素水平线（带高度）</summary>
        protected static void HLine(SpriteBatch sb, int x, int y, int w, int h, Color c) {
            sb.Draw(Px, new Rectangle(x, y, w, h), new Rectangle(0, 0, 1, 1), c);
        }

        /// <summary>像素竖直线</summary>
        protected static void VLine(SpriteBatch sb, int x, int y, int h, Color c) {
            sb.Draw(Px, new Rectangle(x, y, 1, h), new Rectangle(0, 0, 1, 1), c);
        }

        /// <summary>像素竖直线（带宽度）</summary>
        protected static void VLine(SpriteBatch sb, int x, int y, int h, int w, Color c) {
            sb.Draw(Px, new Rectangle(x, y, w, h), new Rectangle(0, 0, 1, 1), c);
        }

        /// <summary>填充矩形</summary>
        internal static void FillRect(SpriteBatch sb, Rectangle rect, Color c) {
            sb.Draw(Px, rect, new Rectangle(0, 0, 1, 1), c);
        }

        /// <summary>矩形线框</summary>
        internal static void StrokeRect(SpriteBatch sb, Rectangle rect, int bw, Color c) {
            sb.Draw(Px, new Rectangle(rect.X, rect.Y, rect.Width, bw), new Rectangle(0, 0, 1, 1), c);
            sb.Draw(Px, new Rectangle(rect.X, rect.Bottom - bw, rect.Width, bw), new Rectangle(0, 0, 1, 1), c);
            sb.Draw(Px, new Rectangle(rect.X, rect.Y, bw, rect.Height), new Rectangle(0, 0, 1, 1), c);
            sb.Draw(Px, new Rectangle(rect.Right - bw, rect.Y, bw, rect.Height), new Rectangle(0, 0, 1, 1), c);
        }

        /// <summary>扩散阴影</summary>
        protected static void DrawShadowLayers(SpriteBatch sb, Rectangle rect, float alpha, int layers, int offX, int offY) {
            for (int d = layers; d >= 1; d--) {
                Rectangle s = rect;
                s.Inflate(d, d);
                s.Offset(offX, offY);
                FillRect(sb, s, Color.Black * (alpha * 0.06f * (layers - d + 1) / (float)layers));
            }
        }

        /// <summary>带居中对齐的文字绘制</summary>
        internal static void DrawCenteredText(SpriteBatch sb, string text, Vector2 center, Color color, float scale) {
            var font = FontAssets.MouseText.Value;
            Vector2 size = font.MeasureString(text) * scale;
            Utils.DrawBorderString(sb, text, center - size / 2f, color, scale);
        }

        /// <summary>条目状态的明文标签，避免只靠符号区分关注、挂起等状态</summary>
        protected static string GetEntryStatusText(QuestEntryStatus status) {
            return status switch {
                QuestEntryStatus.Active => QuestManagerUI.EntryStatusActive?.Value ?? "进行中",
                QuestEntryStatus.Tracked => QuestManagerUI.EntryStatusTracked?.Value ?? "已关注",
                QuestEntryStatus.Suspended => QuestManagerUI.EntryStatusSuspended?.Value ?? "已挂起",
                QuestEntryStatus.Completed => QuestManagerUI.EntryStatusCompleted?.Value ?? "已完成",
                QuestEntryStatus.Failed => QuestManagerUI.EntryStatusFailed?.Value ?? "已失败",
                _ => ""
            };
        }

        protected static int GetStatusBadgeWidth(string statusText, float scale = 0.55f) {
            if (string.IsNullOrEmpty(statusText)) return 0;
            return (int)(FontAssets.MouseText.Value.MeasureString(statusText).X * scale) + 14;
        }

        /// <summary>渐变水平线段</summary>
        protected static void DrawGradientHLine(SpriteBatch sb, int x, int y, int w,
            Color startColor, Color endColor, int segments = 16) {
            for (int i = 0; i < segments; i++) {
                float t = i / (float)segments;
                float t2 = (i + 1) / (float)segments;
                int x1 = x + (int)(t * w);
                int x2 = x + (int)(t2 * w);
                Color c = Color.Lerp(startColor, endColor, t);
                sb.Draw(Px, new Rectangle(x1, y, Math.Max(1, x2 - x1), 1), new Rectangle(0, 0, 1, 1), c);
            }
        }

        /// <summary>渐变进度条</summary>
        protected static void DrawProgressBar(SpriteBatch sb, Rectangle barRect, float progress,
            Color bgColor, Color fillStart, Color fillEnd, Color borderColor, float pulsePhase) {
            FillRect(sb, barRect, bgColor);

            int fillW = (int)(barRect.Width * MathHelper.Clamp(progress, 0f, 1f));
            if (fillW > 2) {
                Rectangle fill = new(barRect.X + 1, barRect.Y + 1, fillW - 2, barRect.Height - 2);
                int segs = 12;
                for (int i = 0; i < segs; i++) {
                    float t = i / (float)segs;
                    float t2 = (i + 1) / (float)segs;
                    int sx1 = fill.X + (int)(t * fill.Width);
                    int sx2 = fill.X + (int)(t2 * fill.Width);
                    Color c = Color.Lerp(fillStart, fillEnd, t);
                    float pulse = MathF.Sin(pulsePhase + t * MathHelper.Pi) * 0.25f + 0.75f;
                    sb.Draw(Px, new Rectangle(sx1, fill.Y, Math.Max(1, sx2 - sx1), fill.Height),
                        new Rectangle(0, 0, 1, 1), c * pulse);
                }
            }

            StrokeRect(sb, barRect, 1, borderColor);
        }

        #endregion
    }
}

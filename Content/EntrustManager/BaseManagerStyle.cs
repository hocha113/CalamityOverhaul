using CalamityOverhaul.Content.UIs.UIEffect;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;

namespace CalamityOverhaul.Content.EntrustManager
{
    /// <summary>样式基类，通用绘制</summary>
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

        public virtual void DrawEmptyHint(SpriteBatch sb, Rectangle contentRect, string text, float alpha) {
            DrawCenteredText(sb, text, contentRect.Center.ToVector2(),
                new Color(60, 150, 220) * (alpha * 0.4f), 0.75f);
        }

        /// <summary>悬停提示默认实现：右下角逐行上叠，旧三套的冷色描边字</summary>
        public virtual void DrawInteractionHints(SpriteBatch sb, Rectangle footerRect,
            EntrustEntryData entry, float alpha) {
            var font = FontAssets.MouseText.Value;
            float hintY = footerRect.Y - 16f;

            string suspendHint = "";
            if (entry.Status == QuestEntryStatus.Active || entry.Status == QuestEntryStatus.Tracked
                || entry.Status == QuestEntryStatus.Suspended)
                suspendHint = QuestManagerUI.SuspendHintText.Value;

            if (!string.IsNullOrEmpty(suspendHint)) {
                float suspendW = font.MeasureString(suspendHint).X * 0.55f;
                Utils.DrawBorderString(sb, suspendHint,
                    new Vector2(footerRect.Right - suspendW - 10f, hintY),
                    new Color(200, 180, 100) * (alpha * 0.5f), 0.55f);
                hintY -= 14f;
            }

            string trackHint = "";
            if (entry.Status == QuestEntryStatus.Active || entry.Status == QuestEntryStatus.Tracked)
                trackHint = QuestManagerUI.TrackHintText.Value;

            if (!string.IsNullOrEmpty(trackHint)) {
                float hintW = font.MeasureString(trackHint).X * 0.55f;
                Utils.DrawBorderString(sb, trackHint,
                    new Vector2(footerRect.Right - hintW - 10f, hintY),
                    new Color(140, 210, 255) * (alpha * 0.5f), 0.55f);
                hintY -= 14f;
            }

            string expandHint = QuestManagerUI.ExpandHintText.Value;
            if (!string.IsNullOrEmpty(expandHint)) {
                float expandW = font.MeasureString(expandHint).X * 0.55f;
                Utils.DrawBorderString(sb, expandHint,
                    new Vector2(footerRect.Right - expandW - 10f, hintY),
                    new Color(120, 200, 180) * (alpha * 0.5f), 0.55f);
            }
        }
        public abstract void DrawQuestEntry(SpriteBatch sb, Rectangle entryRect, EntrustEntryData entry,
            bool isSelected, bool isHovered, float alpha, int entryIndex);
        public abstract void DrawEntrySeparator(SpriteBatch sb, Vector2 start, Vector2 end, float alpha);

        /// <summary>提供者徽记默认框：素圆环 + 纹样，主色描边。悬停名字由行绘制处置</summary>
        public virtual void DrawProviderBadge(SpriteBatch sb, Vector2 center, float radius,
            EntrustEntryData entry, float alpha) {
            EntrustProvider provider = entry?.Provider;
            if (provider == null || radius < 4f || alpha <= 0.01f) {
                return;
            }
            provider.BadgeFill?.Invoke(sb, center, radius, alpha);

            SvgPath ring = SvgPathPen.Path(BadgeRingD);
            SvgPathPen.Stroke(sb, ring, center, radius, 0f, provider.Accent, 1.4f, alpha * 0.85f);
            SvgPath glyph = SvgPathPen.Path(provider.GlyphD);
            if (glyph != null) {
                SvgPathPen.Stroke(sb, glyph, center, radius * 0.62f, 0f,
                    provider.Accent, 1.3f, alpha * 0.95f);
            }

            //悬停报提供者名，徽记不做点击交互
            if (Vector2.Distance(Main.MouseScreen, center) < radius + 3f) {
                Main.hoverItemName = provider.Name?.Value ?? string.Empty;
            }
        }

        /// <summary>整圆，徽记默认框用</summary>
        protected const string BadgeRingD =
            "M 0,-1 C 0.5523,-1 1,-0.5523 1,0 C 1,0.5523 0.5523,1 0,1"
            + " C -0.5523,1 -1,0.5523 -1,0 C -1,-0.5523 -0.5523,-1 0,-1 Z";

        public virtual int GetProviderSignatureHeight(EntrustEntryData entry)
            => entry?.Provider == null ? 0 : 32;

        /// <summary>落款默认版：小头像 + 「委托人 名字」，旧样式的冷色描边字</summary>
        public virtual void DrawProviderSignature(SpriteBatch sb, EntrustEntryData entry,
            float x, float y, float width, float alpha) {
            EntrustProvider provider = entry?.Provider;
            if (provider == null) {
                return;
            }
            Vector2 avatarCenter = new(x + 12f, y + 15f);
            DrawProviderAvatar(sb, provider, avatarCenter, 11f, alpha);

            string label = $"{QuestManagerUI.ProviderLabelText?.Value}  {provider.Name?.Value}";
            Utils.DrawBorderString(sb, label, new Vector2(x + 28f, y + 6f),
                provider.Accent * (alpha * 0.9f), 0.72f);
        }

        /// <summary>头像：物品贴图 → 贴图路径 → 纹样兜底，统一夹进直径盒</summary>
        protected static void DrawProviderAvatar(SpriteBatch sb, EntrustProvider provider,
            Vector2 center, float radius, float alpha) {
            Texture2D tex = null;
            Rectangle frame = default;
            if (provider.AvatarItemType > 0) {
                //原版纹理懒加载，不 LoadItem 只会画出空气
                Main.instance.LoadItem(provider.AvatarItemType);
                tex = TextureAssets.Item[provider.AvatarItemType]?.Value;
                if (tex != null) {
                    frame = Main.itemAnimations[provider.AvatarItemType]?.GetFrame(tex) ?? tex.Frame();
                }
            }
            else if (!string.IsNullOrEmpty(provider.AvatarTexturePath)) {
                tex = CWRUtils.GetT2DAsset(provider.AvatarTexturePath)?.Value;
                if (tex != null) {
                    frame = tex.Frame();
                }
            }

            if (tex != null) {
                float box = radius * 2f;
                float scale = 1f;
                if (frame.Width > box || frame.Height > box) {
                    scale = box / Math.Max(frame.Width, frame.Height);
                }
                sb.Draw(tex, center, frame, Color.White * alpha, 0f, frame.Size() / 2f,
                    scale, SpriteEffects.None, 0f);
                return;
            }

            SvgPath glyph = SvgPathPen.Path(provider.GlyphD);
            if (glyph != null) {
                SvgPathPen.Stroke(sb, glyph, center, radius * 0.85f, 0f,
                    provider.Accent, 1.3f, alpha * 0.95f);
            }
        }
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

        protected static void HLine(SpriteBatch sb, int x, int y, int w, Color c) {
            sb.Draw(Px, new Rectangle(x, y, w, 1), new Rectangle(0, 0, 1, 1), c);
        }

        protected static void HLine(SpriteBatch sb, int x, int y, int w, int h, Color c) {
            sb.Draw(Px, new Rectangle(x, y, w, h), new Rectangle(0, 0, 1, 1), c);
        }

        protected static void VLine(SpriteBatch sb, int x, int y, int h, Color c) {
            sb.Draw(Px, new Rectangle(x, y, 1, h), new Rectangle(0, 0, 1, 1), c);
        }

        protected static void VLine(SpriteBatch sb, int x, int y, int h, int w, Color c) {
            sb.Draw(Px, new Rectangle(x, y, w, h), new Rectangle(0, 0, 1, 1), c);
        }

        internal static void FillRect(SpriteBatch sb, Rectangle rect, Color c) {
            sb.Draw(Px, rect, new Rectangle(0, 0, 1, 1), c);
        }

        internal static void StrokeRect(SpriteBatch sb, Rectangle rect, int bw, Color c) {
            sb.Draw(Px, new Rectangle(rect.X, rect.Y, rect.Width, bw), new Rectangle(0, 0, 1, 1), c);
            sb.Draw(Px, new Rectangle(rect.X, rect.Bottom - bw, rect.Width, bw), new Rectangle(0, 0, 1, 1), c);
            sb.Draw(Px, new Rectangle(rect.X, rect.Y, bw, rect.Height), new Rectangle(0, 0, 1, 1), c);
            sb.Draw(Px, new Rectangle(rect.Right - bw, rect.Y, bw, rect.Height), new Rectangle(0, 0, 1, 1), c);
        }

        protected static void DrawShadowLayers(SpriteBatch sb, Rectangle rect, float alpha, int layers, int offX, int offY) {
            for (int d = layers; d >= 1; d--) {
                Rectangle s = rect;
                s.Inflate(d, d);
                s.Offset(offX, offY);
                FillRect(sb, s, Color.Black * (alpha * 0.06f * (layers - d + 1) / layers));
            }
        }

        internal static void DrawCenteredText(SpriteBatch sb, string text, Vector2 center, Color color, float scale) {
            var font = FontAssets.MouseText.Value;
            Vector2 size = font.MeasureString(text) * scale;
            Utils.DrawBorderString(sb, text, center - size / 2f, color, scale);
        }

        /// <summary>明文状态标签，防只靠符号</summary>
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

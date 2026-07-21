using CalamityOverhaul.Content.HackTimes;
using CalamityOverhaul.Content.UIs.StorageUIs;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.EntrustManager
{
    /// <summary>被关注委托追踪窗口，管理器打开时隐藏</summary>
    internal class EntrustTrackerWidget : UIHandle
    {
        public static EntrustTrackerWidget Instance => UIHandleLoader.GetUIHandleOfType<EntrustTrackerWidget>();

        #region 配置

        private const int WidgetWidth = 220;
        private const int WidgetMinHeight = 80;
        private const int WidgetMaxHeight = 160;
        private const int WidgetMarginLeft = 8;
        private const int WidgetSpacing = 6;
        private const float WidgetPadding = 8f;

        #endregion

        #region 状态

        /// <summary>滑入/滑出进度 0~1</summary>
        private float slideProgress;

        private float collapseProgress;

        /// <summary>折叠则只留标题条</summary>
        private bool isCollapsed;

        private float animTimer;

        /// <summary>NPC 重叠透明度衰减</summary>
        private float overlappingAlpha = 1f;

        private readonly List<EntrustEntryData> trackedEntries = [];

        /// <summary><see cref="Active"/> 可见性查询缓冲，免每帧分配</summary>
        private static readonly List<EntrustEntryData> sharedVisibilityQueryBuffer = [];

        /// <summary>纵向偏移可拖，-1 未初始化</summary>
        private float widgetYOffset = -1f;

        private bool isDragging;

        private float dragAnchor;

        #endregion

        #region UIHandle 生命周期

        public override bool Active {
            get {
                if (Main.gameMenu) return false;
                if (slideProgress > 0.005f) return true;
                if (ShouldTemporarilyHide()) return false;
                var manager = QuestManagerUI.Instance;
                return manager != null && HasVisibleTrackedEntry(manager);
            }
        }

        /// <summary>有关注且自身可见</summary>
        private static bool HasVisibleTrackedEntry(QuestManagerUI manager) {
            var buffer = sharedVisibilityQueryBuffer;
            buffer.Clear();
            manager.GetTrackedEntries(buffer);
            bool any = false;
            for (int i = 0; i < buffer.Count; i++) {
                if (buffer[i].IsTrackerVisible()) {
                    any = true;
                    break;
                }
            }
            buffer.Clear();
            return any;
        }

        public bool IsFullyVisible => slideProgress > 0.85f && trackedEntries.Count > 0;

        /// <summary>可见进度 0~1</summary>
        public float VisibleProgress => slideProgress;

        /// <summary>关注条目外接矩形，供外部定位</summary>
        public Rectangle GetTrackerBounds() {
            if (trackedEntries.Count == 0) return Rectangle.Empty;
            Rectangle union = GetWidgetRect(0);
            for (int i = 1; i < trackedEntries.Count; i++) {
                union = Rectangle.Union(union, GetWidgetRect(i));
            }
            return union;
        }

        public override void OnEnterWorld() {
            slideProgress = 0f;
            collapseProgress = 0f;
            isCollapsed = false;
            animTimer = 0f;
            overlappingAlpha = 1f;
            trackedEntries.Clear();
            isDragging = false;
            //Y偏移存档，-1首次置中
        }

        public override void Update() {
            //首次默认 Y，左侧中上
            if (widgetYOffset < 0f) {
                widgetYOffset = Main.screenHeight * 0.35f;
            }

            RefreshTrackedEntries();

            //有关注且管理器未开
            var manager = QuestManagerUI.Instance;
            bool shouldShow = trackedEntries.Count > 0
                && (manager == null || !manager.IsOpen)
                && !ShouldTemporarilyHide();

            float targetSlide = shouldShow ? 1f : 0f;
            slideProgress = MathHelper.Lerp(slideProgress, targetSlide, 0.15f);
            if (slideProgress < 0.005f && !shouldShow) slideProgress = 0f;
            if (slideProgress > 0.995f && shouldShow) slideProgress = 1f;

            float targetCollapse = isCollapsed ? 1f : 0f;
            collapseProgress = MathHelper.Lerp(collapseProgress, targetCollapse, 0.12f);

            animTimer += 0.016f;
            if (animTimer > MathHelper.TwoPi) animTimer -= MathHelper.TwoPi;

            for (int i = 0; i < trackedEntries.Count; i++) {
                var widgetRect = GetWidgetRect(i);
                trackedEntries[i].TrackerStyle?.Update(widgetRect, slideProgress);
            }

            //近处 NPC 重叠则半透明
            UpdateOverlapAlpha();

            hoverInMainPage = false;
            bool entryConsumedInput = false;
            if (slideProgress > 0.3f) {
                for (int i = 0; i < trackedEntries.Count; i++) {
                    var rect = GetWidgetRect(i);
                    if (rect.Contains(Main.mouseX, Main.mouseY)) {
                        hoverInMainPage = true;
                        //条目先吃输入，防误拖
                        var entry = trackedEntries[i];
                        Rectangle contentRect = new(
                            rect.X + (int)WidgetPadding,
                            rect.Y + 26,
                            rect.Width - (int)(WidgetPadding * 2),
                            rect.Height - 30);
                        if (entry.HandleTrackerInput(rect, contentRect)) {
                            entryConsumedInput = true;
                        }
                        break;
                    }
                }
            }

            if (isDragging) {
                //拖拽中拦截输入，防误开火
                hoverInMainPage = true;
                if (Main.mouseLeft) {
                    widgetYOffset = Main.mouseY - dragAnchor;
                }
                else {
                    isDragging = false;
                }
            }
            else if (hoverInMainPage && !entryConsumedInput && keyLeftPressState == KeyPressState.Pressed) {
                isDragging = true;
                dragAnchor = Main.mouseY - widgetYOffset;
            }

            //每帧夹持 Y，应对分辨率变化
            int totalH = 0;
            for (int i = 0; i < trackedEntries.Count; i++) {
                totalH += GetWidgetHeight(i) + WidgetSpacing;
            }
            totalH = Math.Max(totalH, WidgetMinHeight);
            widgetYOffset = MathHelper.Clamp(widgetYOffset, 0f, Math.Max(0f, Main.screenHeight - totalH));
        }

        #endregion

        #region 数据刷新

        private void RefreshTrackedEntries() {
            trackedEntries.Clear();
            var manager = QuestManagerUI.Instance;
            if (manager == null) return;

            manager.GetTrackedEntries(trackedEntries);

            for (int i = trackedEntries.Count - 1; i >= 0; i--) {
                if (!trackedEntries[i].IsTrackerVisible()) {
                    trackedEntries.RemoveAt(i);
                }
            }
        }

        private static bool ShouldTemporarilyHide() {
            Player player = Main.LocalPlayer;
            if (player == null) return true;

            if (HackTime.Active || HackTime.Intensity > 0.01f)
                return true;

            if (player.chest != -1 || player.talkNPC != -1
                || Main.npcShop > 0 || Main.InGuideCraftMenu)
                return true;

            foreach (var ui in UIHandleLoader.UIHandles) {
                if (ui is BaseChestUI && ui.Active)
                    return true;
            }

            return false;
        }

        #endregion

        #region 矩形计算

        private int GetWidgetWidth(int index) {
            if (index < trackedEntries.Count) {
                int? preferred = trackedEntries[index].TrackerStyle?.GetPreferredWidth();
                if (preferred.HasValue) return preferred.Value;
            }
            return WidgetWidth;
        }

        private Rectangle GetWidgetRect(int index) {
            float eased = VaultUtils.EaseOutCubic(MathHelper.Clamp(slideProgress, 0f, 1f));
            int w = GetWidgetWidth(index);
            int x = (int)MathHelper.Lerp(-w - 10f, WidgetMarginLeft, eased);

            //紧凑可见度由样式控滑入滑出
            if (index < trackedEntries.Count) {
                var entry = trackedEntries[index];
                int? compactH = entry.TrackerStyle?.GetIdleCompactHeight(entry);
                if (compactH.HasValue) {
                    float cv = entry.TrackerStyle?.GetCompactVisibility(entry) ?? 1f;
                    cv = VaultUtils.EaseOutCubic(MathHelper.Clamp(cv, 0f, 1f));
                    x = (int)MathHelper.Lerp(-w - 10f, x, cv);
                }
            }

            int y = (int)widgetYOffset;
            for (int i = 0; i < index; i++) {
                y += GetWidgetHeight(i) + WidgetSpacing;
            }

            int h = GetWidgetHeight(index);

            float collapse = VaultUtils.EaseInOutCubic(MathHelper.Clamp(collapseProgress, 0f, 1f));
            int collapsedH = 24;
            h = (int)MathHelper.Lerp(h, collapsedH, collapse);

            return new Rectangle(x, y, w, h);
        }

        private int GetWidgetHeight(int index) {
            if (index >= trackedEntries.Count) return WidgetMinHeight;
            var entry = trackedEntries[index];
            int? custom = entry.TrackerStyle?.GetMinHeight();
            int baseH = custom ?? WidgetMinHeight;

            //紧凑高由样式判定
            int? compactH = entry.TrackerStyle?.GetIdleCompactHeight(entry);
            if (compactH.HasValue) {
                return compactH.Value + (int)entry.GetTrackerContentTopPadding();
            }

            var details = entry.GetTrackerDetails();
            var font = FontAssets.MouseText.Value;
            int w = GetWidgetWidth(index);
            int wrapWidth = (int)((w - WidgetPadding * 2) / 0.6f);
            int contentH = 30;
            foreach (string line in details) {
                string[] wrapped = VaultUtils.WrapTextArray(line, font, wrapWidth, 99, out _);
                foreach (string wl in wrapped) {
                    if (string.IsNullOrEmpty(wl)) continue;
                    contentH += (int)(font.MeasureString(wl.TrimEnd('-', ' ')).Y * 0.6f) + 2;
                }
            }
            if (entry.Progress > 0f && entry.Status != QuestEntryStatus.Completed) {
                contentH += 20; ; // 进度条
            }
            contentH += 16; ; // 底部边距
            contentH += (int)entry.GetTrackerContentTopPadding(); ; // 顶部间距
            //条目额外高，按钮等
            contentH += Math.Max(0, entry.GetTrackerExtraHeight());

            return Math.Clamp(contentH, baseH, WidgetMaxHeight);
        }

        #endregion

        #region 绘制

        public override void Draw(SpriteBatch spriteBatch) {
            if (slideProgress <= 0.005f) return;

            float alpha = slideProgress * overlappingAlpha;
            var font = FontAssets.MouseText.Value;

            for (int i = 0; i < trackedEntries.Count; i++) {
                var entry = trackedEntries[i];
                Rectangle widgetRect = GetWidgetRect(i);

                if (widgetRect.Bottom < 0 || widgetRect.Y > Main.screenHeight) continue;

                var style = entry.TrackerStyle;

                if (style != null) {
                    style.DrawWidgetBackground(spriteBatch, widgetRect, alpha);
                }
                else {
                    DrawDefaultBackground(spriteBatch, widgetRect, alpha);
                }

                if (style != null) {
                    style.DrawWidgetFrame(spriteBatch, widgetRect, alpha);
                }
                else {
                    DrawDefaultFrame(spriteBatch, widgetRect, alpha);
                }

                Rectangle headerRect = new(widgetRect.X, widgetRect.Y, widgetRect.Width, 24);
                if (style != null) {
                    style.DrawWidgetHeader(spriteBatch, headerRect, entry.Title ?? "", alpha);
                }
                else {
                    DrawDefaultHeader(spriteBatch, headerRect, entry.Title ?? "", alpha);
                }

                if (collapseProgress > 0.95f) continue;

                float contentAlpha = alpha * (1f - collapseProgress);

                Rectangle contentRect = new(
                    widgetRect.X + (int)WidgetPadding,
                    widgetRect.Y + 26,
                    widgetRect.Width - (int)(WidgetPadding * 2),
                    widgetRect.Height - 30);

                if (!entry.DrawTrackerContent(spriteBatch, contentRect, contentAlpha)) {
                    //默认文字行+进度条
                    DrawDefaultContent(spriteBatch, contentRect, entry, style, contentAlpha);
                }

                style?.DrawWidgetOverlay(spriteBatch, widgetRect, alpha);
            }
        }

        #endregion

        #region 默认绘制

        private void DrawDefaultBackground(SpriteBatch sb, Rectangle rect, float alpha) {
            BaseManagerStyle.FillRect(sb, rect, new Color(4, 8, 18) * (alpha * 0.85f));
        }

        private void DrawDefaultFrame(SpriteBatch sb, Rectangle rect, float alpha) {
            var px = VaultAsset.placeholder2.Value;
            Color frameC = new Color(60, 150, 220) * (alpha * 0.4f);
            sb.Draw(px, new Rectangle(rect.X, rect.Y, rect.Width, 2), frameC);
            sb.Draw(px, new Rectangle(rect.X, rect.Y, 2, rect.Height), frameC * 0.6f);
            sb.Draw(px, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), frameC * 0.3f);
            sb.Draw(px, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height), frameC * 0.2f);
        }

        private void DrawDefaultHeader(SpriteBatch sb, Rectangle headerRect, string title, float alpha) {
            BaseManagerStyle.FillRect(sb, headerRect, new Color(8, 16, 32) * (alpha * 0.5f));
            var font = FontAssets.MouseText.Value;
            float maxTitleW = headerRect.Width - 16f;
            if (font.MeasureString(title).X * 0.72f > maxTitleW) {
                while (title.Length > 3 && font.MeasureString(title + "...").X * 0.72f > maxTitleW)
                    title = title[..^1];
                title += "...";
            }
            Color titleC = new Color(140, 210, 255) * alpha;
            Utils.DrawBorderString(sb, title,
                new Vector2(headerRect.X + 8f, headerRect.Y + (headerRect.Height - 16f) / 2f),
                titleC, 0.72f);
            var px = VaultAsset.placeholder2.Value;
            sb.Draw(px, new Rectangle(headerRect.X + 4, headerRect.Bottom - 1, headerRect.Width - 8, 1),
                new Color(60, 150, 220) * (alpha * 0.3f));
        }

        private void DrawDefaultContent(SpriteBatch sb, Rectangle contentRect, EntrustEntryData entry,
            IEntrustTrackerWidgetStyle style, float alpha) {
            var font = FontAssets.MouseText.Value;
            Color textC = style?.GetWidgetTextColor(alpha) ?? new Color(160, 190, 210) * (alpha * 0.8f);
            Color accentC = style?.GetWidgetAccentColor(alpha) ?? new Color(80, 255, 220) * alpha;

            float y = contentRect.Y + entry.GetTrackerContentTopPadding();

            var details = entry.GetTrackerDetails();
            int wrapWidth = (int)(contentRect.Width / 0.6f);
            foreach (string line in details) {
                if (y + 16 > contentRect.Bottom) break;
                string[] wrapped = VaultUtils.WrapTextArray(line, font, wrapWidth, 99, out _);
                foreach (string wl in wrapped) {
                    if (string.IsNullOrEmpty(wl)) continue;
                    if (y + 16 > contentRect.Bottom) break;
                    string trimmed = wl.TrimEnd('-', ' ');
                    Utils.DrawBorderString(sb, trimmed, new Vector2(contentRect.X, y), textC, 0.6f);
                    y += (int)(font.MeasureString(trimmed).Y * 0.6f) + 2;
                }
            }

            if (entry.Progress > 0f && entry.Status != QuestEntryStatus.Completed) {
                y += 3f;

                if (style != null) {
                    style.DrawWidgetDivider(sb,
                        new Vector2(contentRect.X, y),
                        new Vector2(contentRect.Right - 4, y), alpha);
                }
                y += 4f;

                int barW = contentRect.Width - 4;
                Rectangle barRect = new(contentRect.X, (int)y, barW, 5);
                if (style != null) {
                    style.DrawWidgetProgress(sb, barRect, entry.Progress,
                        entry.ProgressText, alpha);
                }
                else {
                    BaseManagerStyle.FillRect(sb, barRect, new Color(8, 16, 32) * alpha);
                    int fillW = (int)(barW * MathHelper.Clamp(entry.Progress, 0f, 1f));
                    if (fillW > 0) {
                        BaseManagerStyle.FillRect(sb, new Rectangle(barRect.X, barRect.Y, fillW, 5), accentC * 0.8f);
                    }

                    if (entry.ProgressText != null) {
                        Utils.DrawBorderString(sb, entry.ProgressText,
                            new Vector2(barRect.Right - font.MeasureString(entry.ProgressText).X * 0.5f - 2f,
                                barRect.Bottom + 2f),
                            accentC * 0.7f, 0.5f);
                    }
                }
            }
        }

        #endregion

        #region NPC重叠透明化

        private void UpdateOverlapAlpha() {
            bool overlapping = false;
            for (int i = 0; i < trackedEntries.Count && !overlapping; i++) {
                Rectangle wRect = GetWidgetRect(i);
                for (int n = 0; n < Main.maxNPCs; n++) {
                    NPC npc = Main.npc[n];
                    if (!npc.active || npc.friendly) continue;
                    Vector2 screen = npc.Center - Main.screenPosition;
                    if (wRect.Contains((int)screen.X, (int)screen.Y)) {
                        overlapping = true;
                        break;
                    }
                }
            }

            float target = overlapping ? 0.3f : 1f;
            overlappingAlpha = MathHelper.Lerp(overlappingAlpha, target, 0.08f);
        }

        #endregion

        #region 存档

        public override void SaveUIData(TagCompound tag) {
            tag[Name + ":widgetYOffset"] = widgetYOffset;
        }

        public override void LoadUIData(TagCompound tag) {
            if (tag.TryGet(Name + ":widgetYOffset", out float y))
                widgetYOffset = y;
        }

        #endregion
    }
}

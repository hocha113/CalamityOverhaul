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

        /// <summary>折叠进度</summary>
        private float collapseProgress;

        /// <summary>是否折叠（全部收起只显示标题条）</summary>
        private bool isCollapsed;

        /// <summary>全局动画计时器</summary>
        private float animTimer;

        /// <summary>NPC重叠时的透明度衰减</summary>
        private float overlappingAlpha = 1f;

        /// <summary>缓存的被关注条目引用</summary>
        private readonly List<EntrustEntryData> trackedEntries = [];

        /// <summary><see cref="Active"/> 中查询可见性时复用的临时缓冲，避免每帧分配</summary>
        private static readonly List<EntrustEntryData> sharedVisibilityQueryBuffer = [];

        /// <summary>窗口纵向偏移（可拖拽），-1 表示尚未初始化</summary>
        private float widgetYOffset = -1f;

        /// <summary>是否正在拖拽</summary>
        private bool isDragging;

        /// <summary>拖拽起始时鼠标与 widgetYOffset 的差</summary>
        private float dragAnchor;



        #endregion

        #region UIHandle 生命周期

        /// <summary>当存在被关注的委托且管理器未打开时显示</summary>
        public override bool Active {
            get {
                if (Main.gameMenu) return false;
                //动画尚未结束时保持激活
                if (slideProgress > 0.005f) return true;
                if (ShouldTemporarilyHide()) return false;
                //查管理器源数据并叠加 IsTrackerVisible 过滤
                var manager = QuestManagerUI.Instance;
                return manager != null && HasVisibleTrackedEntry(manager);
            }
        }

        /// <summary>是否存在至少一条同时满足"被关注 + 自身可见性条件"的条目</summary>
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

        /// <summary>追踪栏当前是否已基本完全展示</summary>
        public bool IsFullyVisible => slideProgress > 0.85f && trackedEntries.Count > 0;

        /// <summary>追踪栏当前可见性进度，0~1</summary>
        public float VisibleProgress => slideProgress;

        /// <summary>获取所有被关注条目的整体外接矩形，用于外部 UI 定位</summary>
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
            //widgetYOffset 保留存档值，-1 在首次 Update 中初始化为屏幕中部
        }

        public override void Update() {
            //首次初始化默认 Y 位置：屏幕左侧中间偏上
            if (widgetYOffset < 0f) {
                widgetYOffset = Main.screenHeight * 0.35f;
            }

            //刷新被关注条目列表
            RefreshTrackedEntries();

            //目标状态：有被关注的条目且管理器未打开
            var manager = QuestManagerUI.Instance;
            bool shouldShow = trackedEntries.Count > 0
                && (manager == null || !manager.IsOpen)
                && !ShouldTemporarilyHide();

            float targetSlide = shouldShow ? 1f : 0f;
            slideProgress = MathHelper.Lerp(slideProgress, targetSlide, 0.15f);
            if (slideProgress < 0.005f && !shouldShow) slideProgress = 0f;
            if (slideProgress > 0.995f && shouldShow) slideProgress = 1f;

            //折叠动画
            float targetCollapse = isCollapsed ? 1f : 0f;
            collapseProgress = MathHelper.Lerp(collapseProgress, targetCollapse, 0.12f);

            //动画计时器
            animTimer += 0.016f;
            if (animTimer > MathHelper.TwoPi) animTimer -= MathHelper.TwoPi;

            //更新每个条目的追踪窗口样式
            for (int i = 0; i < trackedEntries.Count; i++) {
                var widgetRect = GetWidgetRect(i);
                trackedEntries[i].TrackerStyle?.Update(widgetRect, slideProgress);
            }

            //NPC重叠检测，追踪窗口与近处NPC重叠时半透明
            UpdateOverlapAlpha();

            //鼠标交互
            hoverInMainPage = false;
            bool entryConsumedInput = false;
            if (slideProgress > 0.3f) {
                for (int i = 0; i < trackedEntries.Count; i++) {
                    var rect = GetWidgetRect(i);
                    if (rect.Contains(Main.mouseX, Main.mouseY)) {
                        hoverInMainPage = true;
                        //条目先处理输入，避免误触拖拽
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

            //纵向拖拽
            if (isDragging) {
                //拖拽过程中强制保持输入拦截，防止误触发武器攻击
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

            //每帧夹持 Y 偏移到当前屏幕范围内（应对分辨率变化）
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

            //叠加条目可见性过滤
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

            //由样式自行决定紧凑条目的可见度，控制滑入/滑出
            if (index < trackedEntries.Count) {
                var entry = trackedEntries[index];
                int? compactH = entry.TrackerStyle?.GetIdleCompactHeight(entry);
                if (compactH.HasValue) {
                    float cv = entry.TrackerStyle?.GetCompactVisibility(entry) ?? 1f;
                    cv = VaultUtils.EaseOutCubic(MathHelper.Clamp(cv, 0f, 1f));
                    x = (int)MathHelper.Lerp(-w - 10f, x, cv);
                }
            }

            //纵向排列，从 widgetYOffset 开始
            int y = (int)widgetYOffset;
            for (int i = 0; i < index; i++) {
                y += GetWidgetHeight(i) + WidgetSpacing;
            }

            int h = GetWidgetHeight(index);

            //折叠时高度缩到标题行
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

            //紧凑模式：由样式自行判定是否启用并返回紧凑高度
            int? compactH = entry.TrackerStyle?.GetIdleCompactHeight(entry);
            if (compactH.HasValue) {
                return compactH.Value + (int)entry.GetTrackerContentTopPadding();
            }

            //根据内容行数动态调整高度（考虑换行）
            var details = entry.GetTrackerDetails();
            var font = FontAssets.MouseText.Value;
            int w = GetWidgetWidth(index);
            int wrapWidth = (int)((w - WidgetPadding * 2) / 0.6f);
            int contentH = 30;
            foreach (string line in details) {
                string[] wrapped = CWRUtils.WrapTextArray(line, font, wrapWidth, 99, out _);
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
            //条目自定义额外高度（用于容纳按钮等元素）
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

                //可见性裁剪
                if (widgetRect.Bottom < 0 || widgetRect.Y > Main.screenHeight) continue;

                var style = entry.TrackerStyle;

                //背景
                if (style != null) {
                    style.DrawWidgetBackground(spriteBatch, widgetRect, alpha);
                }
                else {
                    DrawDefaultBackground(spriteBatch, widgetRect, alpha);
                }

                //边框
                if (style != null) {
                    style.DrawWidgetFrame(spriteBatch, widgetRect, alpha);
                }
                else {
                    DrawDefaultFrame(spriteBatch, widgetRect, alpha);
                }

                //标题
                Rectangle headerRect = new(widgetRect.X, widgetRect.Y, widgetRect.Width, 24);
                if (style != null) {
                    style.DrawWidgetHeader(spriteBatch, headerRect, entry.Title ?? "", alpha);
                }
                else {
                    DrawDefaultHeader(spriteBatch, headerRect, entry.Title ?? "", alpha);
                }

                //折叠时只画标题
                if (collapseProgress > 0.95f) continue;

                float contentAlpha = alpha * (1f - collapseProgress);

                //内容区
                Rectangle contentRect = new(
                    widgetRect.X + (int)WidgetPadding,
                    widgetRect.Y + 26,
                    widgetRect.Width - (int)(WidgetPadding * 2),
                    widgetRect.Height - 30);

                //让条目自定义绘制
                if (!entry.DrawTrackerContent(spriteBatch, contentRect, contentAlpha)) {
                    //默认绘制：文字行 + 进度条
                    DrawDefaultContent(spriteBatch, contentRect, entry, style, contentAlpha);
                }

                //前景特效
                style?.DrawWidgetOverlay(spriteBatch, widgetRect, alpha);
            }
        }

        #endregion

        #region 默认绘制

        private void DrawDefaultBackground(SpriteBatch sb, Rectangle rect, float alpha) {
            //深色半透明背景
            BaseManagerStyle.FillRect(sb, rect, new Color(4, 8, 18) * (alpha * 0.85f));
        }

        private void DrawDefaultFrame(SpriteBatch sb, Rectangle rect, float alpha) {
            var px = VaultAsset.placeholder2.Value;
            Color frameC = new Color(60, 150, 220) * (alpha * 0.4f);
            //顶部线
            sb.Draw(px, new Rectangle(rect.X, rect.Y, rect.Width, 2), frameC);
            //左侧线
            sb.Draw(px, new Rectangle(rect.X, rect.Y, 2, rect.Height), frameC * 0.6f);
            //底部线
            sb.Draw(px, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), frameC * 0.3f);
            //右侧线
            sb.Draw(px, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height), frameC * 0.2f);
        }

        private void DrawDefaultHeader(SpriteBatch sb, Rectangle headerRect, string title, float alpha) {
            //标题栏背景
            BaseManagerStyle.FillRect(sb, headerRect, new Color(8, 16, 32) * (alpha * 0.5f));
            //标题文字，超宽截断
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
            //底部分隔
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

            //详情行换行
            var details = entry.GetTrackerDetails();
            int wrapWidth = (int)(contentRect.Width / 0.6f);
            foreach (string line in details) {
                if (y + 16 > contentRect.Bottom) break;
                string[] wrapped = CWRUtils.WrapTextArray(line, font, wrapWidth, 99, out _);
                foreach (string wl in wrapped) {
                    if (string.IsNullOrEmpty(wl)) continue;
                    if (y + 16 > contentRect.Bottom) break;
                    string trimmed = wl.TrimEnd('-', ' ');
                    Utils.DrawBorderString(sb, trimmed, new Vector2(contentRect.X, y), textC, 0.6f);
                    y += (int)(font.MeasureString(trimmed).Y * 0.6f) + 2;
                }
            }

            //分隔线 + 进度条
            if (entry.Progress > 0f && entry.Status != QuestEntryStatus.Completed) {
                y += 3f;

                //分隔线
                if (style != null) {
                    style.DrawWidgetDivider(sb,
                        new Vector2(contentRect.X, y),
                        new Vector2(contentRect.Right - 4, y), alpha);
                }
                y += 4f;

                //进度条
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

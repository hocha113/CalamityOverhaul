using CalamityOverhaul.Content.EntrustManager.Styles;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.EntrustManager
{
    internal class QuestManagerSysteam : ModSystem
    {
        public override void OnWorldUnload() {
            QuestManagerUI.Instance?.ClearAll();
        }
    }

    /// <summary>委托管理器主界面</summary>
    internal class QuestManagerUI : UIHandle, ILocalizedModType
    {
        public string LocalizationCategory => "UI";
        public static QuestManagerUI Instance => UIHandleLoader.GetUIHandleOfType<QuestManagerUI>();

        #region 本地化

        public static LocalizedText TitleText { get; private set; }
        public static LocalizedText CategoryAll { get; private set; }
        public static LocalizedText CategoryActive { get; private set; }
        public static LocalizedText CategoryCompleted { get; private set; }
        public static LocalizedText CategorySuspended { get; private set; }
        public static LocalizedText EmptyHintText { get; private set; }
        public static LocalizedText TrackHintText { get; private set; }
        public static LocalizedText SuspendHintText { get; private set; }
        public static LocalizedText ExpandHintText { get; private set; }
        public static LocalizedText HeaderStatusTag { get; private set; }
        public static LocalizedText FooterStatsFormat { get; private set; }
        public static LocalizedText EntryStatusActive { get; private set; }
        public static LocalizedText EntryStatusTracked { get; private set; }
        public static LocalizedText EntryStatusSuspended { get; private set; }
        public static LocalizedText EntryStatusCompleted { get; private set; }
        public static LocalizedText EntryStatusFailed { get; private set; }
        public static LocalizedText ProviderLabelText { get; private set; }

        public override void SetStaticDefaults() {
            EntrustProviders.InitLocalization(this);
            ProviderLabelText = this.GetLocalization(nameof(ProviderLabelText), () => "委托人");
            TitleText = this.GetLocalization(nameof(TitleText), () => "任务管理");
            CategoryAll = this.GetLocalization(nameof(CategoryAll), () => "全部");
            CategoryActive = this.GetLocalization(nameof(CategoryActive), () => "进行中");
            CategoryCompleted = this.GetLocalization(nameof(CategoryCompleted), () => "已完成");
            CategorySuspended = this.GetLocalization(nameof(CategorySuspended), () => "挂起");
            EmptyHintText = this.GetLocalization(nameof(EmptyHintText), () => "暂无任务...");
            TrackHintText = this.GetLocalization(nameof(TrackHintText), () => "[右键] 关注/取消关注");
            SuspendHintText = this.GetLocalization(nameof(SuspendHintText), () => "[中键] 挂起/恢复");
            ExpandHintText = this.GetLocalization(nameof(ExpandHintText), () => "[左键] 展开/收起详情");
            HeaderStatusTag = this.GetLocalization(nameof(HeaderStatusTag), () => "◈ ACTIVE");
            FooterStatsFormat = this.GetLocalization(nameof(FooterStatsFormat), () => "TOTAL: {0}  |  ACTIVE: {1}");
            EntryStatusActive = this.GetLocalization(nameof(EntryStatusActive), () => "进行中");
            EntryStatusTracked = this.GetLocalization(nameof(EntryStatusTracked), () => "已关注");
            EntryStatusSuspended = this.GetLocalization(nameof(EntryStatusSuspended), () => "已挂起");
            EntryStatusCompleted = this.GetLocalization(nameof(EntryStatusCompleted), () => "已完成");
            EntryStatusFailed = this.GetLocalization(nameof(EntryStatusFailed), () => "已失败");
        }

        #endregion

        #region 状态与配置

        /// <summary>
        /// 委托卷宗已内嵌进任务书，"打开"等价于任务书正翻在委托站点。<br/>
        /// 引导与外部联动读这一个来源
        /// </summary>
        public new bool IsOpen => QuestLogs.QuestLog.Instance?.EntrustViewActive == true;

        /// <summary>内容区右缘 X，供引导卡定位</summary>
        public int PanelRightEdge { get; private set; }

        /// <summary>内嵌内容区，由任务书每帧交付</summary>
        private Rectangle hostRect;

        private float contentAlpha;

        private const int TabBarHeight = 28;

        private const int FooterHeight = 26;

        private const int ScrollbarWidth = 8;

        #endregion

        #region 滚动与选中

        private float scrollOffset;
        private float scrollTarget;
        private int selectedIndex = -1;
        private int hoveredIndex = -1;
        private int selectedCategoryIndex;
        private bool filterDirty = true;
        /// <summary>上一帧中键按下，检点击沿</summary>
        private bool prevMiddleDown;

        private readonly string[] categoryKeys = ["Active", "All", "Completed", "Suspended"];
        private string[] categoryNames;

        private static readonly RasterizerState ScissorRaster = new() { ScissorTestEnable = true };

        #endregion

        #region 任务数据

        private readonly List<EntrustEntryData> allEntries = [];

        private readonly List<EntrustEntryData> filteredEntries = [];

        public void RegisterQuest(EntrustEntryData entry) {
            if (allEntries.All(e => e.Key != entry.Key)) {
                //新注册 Active→关注
                if (entry.Status == QuestEntryStatus.Active) {
                    entry.Status = QuestEntryStatus.Tracked;
                    EntrustManagerNotification.Notify(entry.Title,
                        EntrustManagerNotification.NotifyKind.NewQuest);
                }
                else if (entry.Status == QuestEntryStatus.Suspended) {
                    EntrustManagerNotification.Notify(entry.Title,
                        EntrustManagerNotification.NotifyKind.Suspended);
                }
                allEntries.Add(entry);
                filterDirty = true;
            }
        }

        public void UnregisterQuest(string key) {
            if (allEntries.RemoveAll(e => e.Key == key) > 0)
                filterDirty = true;
        }

        public EntrustEntryData GetEntry(string key) {
            return allEntries.Find(e => e.Key == key);
        }

        public override void UnLoad() {
            //提供者实例持有本地化引用，卸载时放掉
            EntrustProviders.UnloadInstances();
        }

        public void ClearAll() {
            allEntries.Clear();
            filteredEntries.Clear();
            selectedIndex = -1;
            hoveredIndex = -1;
            scrollOffset = 0f;
            scrollTarget = 0f;
            filterDirty = true;
        }

        public void MarkFilterDirty() => filterDirty = true;

        /// <summary>改状态并通知、刷过滤</summary>
        public bool SetEntryStatus(string key, QuestEntryStatus newStatus, float? progress = null) {
            var entry = GetEntry(key);
            if (entry == null || entry.Status == newStatus) return false;

            if (progress.HasValue) entry.Progress = progress.Value;
            return ChangeEntryStatus(entry, newStatus);
        }

        /// <summary>关注条目，供 <see cref="EntrustTrackerWidget"/></summary>
        public void GetTrackedEntries(List<EntrustEntryData> result) {
            foreach (var e in allEntries) {
                if (e.Status == QuestEntryStatus.Tracked)
                    result.Add(e);
            }
        }

        public bool HasAnyEntry => allEntries.Count > 0;

        public bool HasTrackedEntries() {
            foreach (var e in allEntries) {
                if (e.Status == QuestEntryStatus.Tracked)
                    return true;
            }
            return false;
        }

        /// <summary>按状态计数，引导兜底</summary>
        public int CountByStatus(QuestEntryStatus status) {
            int n = 0;
            foreach (var e in allEntries) {
                if (e.Status == status) n++;
            }
            return n;
        }

        /// <summary>兜底关注 Key，Active 优先再 Suspended</summary>
        public string TryGetFirstTrackableKey() {
            foreach (var e in allEntries) {
                if (e.Status == QuestEntryStatus.Active) return e.Key;
            }
            foreach (var e in allEntries) {
                if (e.Status == QuestEntryStatus.Suspended) return e.Key;
            }
            return null;
        }

        /// <summary>兜底挂起 Key，优先 Active</summary>
        public string TryGetFirstSuspendableKey() {
            foreach (var e in allEntries) {
                if (e.Status == QuestEntryStatus.Active) return e.Key;
            }
            return null;
        }

        /// <summary>
        /// 教程开讲前把分类拉回「进行中」。存档可能停在已完成/挂起页，
        /// 那里没有可讲的样本行，教程会对着空列表念
        /// </summary>
        public void ResetCategoryForGuide() {
            if (selectedCategoryIndex == 0) {
                return;
            }
            selectedCategoryIndex = 0;
            scrollTarget = 0f;
            scrollOffset = 0f;
            selectedIndex = -1;
            filterDirty = true;
        }

        #endregion

        #region 引导定位（只读）

        //教程要圈出真实的行与页签，几何只此一份，别让引导另算一遍

        /// <summary>分类选项卡带；书没翻到委托站点时为空</summary>
        public Rectangle CategoryTabRect => hostRect.Width > 0 ? GetTabRect(hostRect) : Rectangle.Empty;

        /// <summary>条目列表可视区</summary>
        public Rectangle EntryListRect => hostRect.Width > 0 ? GetContentRect(hostRect) : Rectangle.Empty;

        /// <summary>当前分类下的首条委托，教程拿它当讲解样本</summary>
        public EntrustEntryData FirstVisibleEntry => filteredEntries.Count > 0 ? filteredEntries[0] : null;

        /// <summary>第 index 条可见条目的行矩形，已夹进可视区；整行滚出视口时返回 false</summary>
        public bool TryGetEntryRect(int index, out Rectangle rect) {
            rect = Rectangle.Empty;
            if (hostRect.Width <= 0 || index < 0 || index >= filteredEntries.Count) {
                return false;
            }
            Rectangle content = GetContentRect(hostRect);
            float top = content.Y + GetEntryYOffset(index) - scrollOffset;
            float bottom = top + GetDynamicEntryHeight(filteredEntries[index]);
            if (bottom <= content.Y || top >= content.Bottom) {
                return false;
            }
            int clampedTop = (int)MathF.Max(top, content.Y);
            int clampedBottom = (int)MathF.Min(bottom, content.Bottom);
            rect = new Rectangle(content.X, clampedTop, content.Width,
                Math.Max(1, clampedBottom - clampedTop));
            return true;
        }

        #endregion

        #region 样式系统

        private IEntrustManagerStyle currentStyle;
        private readonly List<IEntrustManagerStyle> availableStyles = [];
        private int currentStyleIndex;

        public void SetStyle(IEntrustManagerStyle style) {
            currentStyle?.Reset();
            currentStyle = style;
        }

        /// <summary>按索引设样式，sync 同步任务书</summary>
        public void SetStyleByIndex(int index, bool sync = true) {
            if (availableStyles.Count == 0) return;
            currentStyleIndex = Math.Clamp(index, 0, availableStyles.Count - 1);
            SetStyle(availableStyles[currentStyleIndex]);
            if (sync) {
                QuestLogs.QuestLog.Instance?.SetStyleByIndex(currentStyleIndex, false);
            }
        }

        #endregion

        #region 动画

        private float edgeGlowPhase;

        #endregion

        #region UIHandle 生命周期

        //本体不再自绘面板，但仍需每帧推进条目与过滤，故与条目共存亡
        public override bool Active => !Main.gameMenu && (IsOpen || allEntries.Count > 0);

        public QuestManagerUI() {
            //序号与任务书的样式表一一对应，不可重排
            availableStyles.Add(new HotwindManagerStyle());
            availableStyles.Add(new DraedonManagerStyle());
            availableStyles.Add(new ForestManagerStyle());
            availableStyles.Add(new ChronicleManagerStyle());
            currentStyleIndex = QuestLogs.QuestLog.ChronicleStyleIndex;
            currentStyle = availableStyles[currentStyleIndex];
            categoryNames = new string[4];
        }

        public override void OnEnterWorld() {
            contentAlpha = 0f;
            scrollOffset = 0f;
            scrollTarget = 0f;
            selectedIndex = -1;
            hoveredIndex = -1;
            selectedCategoryIndex = 0;
            edgeGlowPhase = 0f;
            currentStyle?.Reset();

            categoryNames = [
                CategoryActive.Value,
                CategoryAll.Value,
                CategoryCompleted.Value,
                CategorySuspended.Value
            ];

        }

        public override void LogicUpdate() {
            currentStyle?.Update(hostRect, IsOpen ? 1f : 0f);
        }

        /// <summary>只推进与开合无关的记账：条目、展开动画、过滤重建</summary>
        public override void Update() {
            float contentTarget = IsOpen ? 1f : 0f;
            contentAlpha = MathHelper.Lerp(contentAlpha, contentTarget, 0.15f);

            edgeGlowPhase += 0.03f;
            if (edgeGlowPhase > MathHelper.TwoPi) edgeGlowPhase -= MathHelper.TwoPi;

            scrollOffset = MathHelper.Lerp(scrollOffset, scrollTarget, 0.18f);

            if (filterDirty) {
                RebuildFilteredEntries();
                filterDirty = false;
            }

            foreach (var entry in allEntries) {
                entry.OnUpdate();

                float expandTarget = entry.IsExpanded ? 1f : 0f;
                entry.ExpandProgress = MathHelper.Lerp(entry.ExpandProgress, expandTarget, 0.14f);
                if (entry.ExpandProgress < 0.005f) entry.ExpandProgress = 0f;
                if (entry.ExpandProgress > 0.995f) entry.ExpandProgress = 1f;
            }

            if (!IsOpen) {
                hoveredIndex = -1;
                hoverInMainPage = false;
            }
        }

        /// <summary>
        /// 内嵌态输入，由任务书在委托站点每帧调用。<br/>
        /// 指针占用与滚轮锁由任务书统一负责，此处只管列表自身
        /// </summary>
        public void UpdateEmbedded(Rectangle host, bool chromeHovered, int scrollDelta) {
            hostRect = host;
            PanelRightEdge = host.Right;
            UIHitBox = host;
            hoverInMainPage = host.Intersects(MouseHitBox) && !chromeHovered;

            if (hoverInMainPage) {
                if (scrollDelta != 0) {
                    scrollTarget -= scrollDelta * 0.3f;
                    ClampScroll(host);
                }
                HandleMouseInput(host);
            }

            //中键态每帧更新，防跨帧漂移
            prevMiddleDown = Mouse.GetState().MiddleButton == ButtonState.Pressed;
        }

        #endregion

        #region 开关与交互

        /// <summary>入口已并入任务书：翻到委托站点，或在该站点时合书</summary>
        public void TogglePanel() {
            QuestLogs.QuestLog.Instance?.ToggleEntrustView();
        }

        private void HandleMouseInput(Rectangle panelRect) {
            Rectangle contentRect = GetContentRect(panelRect);
            int padding = currentStyle?.GetEntryPadding() ?? 4;

            Rectangle tabRect = GetTabRect(panelRect);
            if (tabRect.Contains(Main.mouseX, Main.mouseY)) {
                if (keyLeftPressState == KeyPressState.Pressed) {
                    int tabIndex = GetTabIndexAtX(tabRect, Main.mouseX);
                    if (tabIndex >= 0 && tabIndex != selectedCategoryIndex) {
                        selectedCategoryIndex = tabIndex;
                        scrollTarget = 0f;
                        scrollOffset = 0f;
                        selectedIndex = -1;
                        filterDirty = true;
                        SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.35f });
                    }
                }
                return;
            }

            hoveredIndex = -1;
            if (contentRect.Contains(Main.mouseX, Main.mouseY)) {
                float relativeY = Main.mouseY - contentRect.Y + scrollOffset;
                int idx = -1;
                float accY = 0f;
                for (int i = 0; i < filteredEntries.Count; i++) {
                    float entryH = GetDynamicEntryHeight(filteredEntries[i]) + padding;
                    if (relativeY >= accY && relativeY < accY + entryH - padding) {
                        idx = i;
                        break;
                    }
                    accY += entryH;
                }

                if (idx >= 0 && idx < filteredEntries.Count) {
                    hoveredIndex = idx;

                    if (keyLeftPressState == KeyPressState.Pressed) {
                        var entry = filteredEntries[idx];
                        foreach (var other in filteredEntries) {
                            if (other != entry && other.IsExpanded)
                                other.IsExpanded = false;
                        }
                        entry.IsExpanded = !entry.IsExpanded;
                        selectedIndex = entry.IsExpanded ? idx : -1;

                        //展开后滚入可视区
                        if (entry.IsExpanded) {
                            float entryTop = GetEntryYOffset(idx);
                            int expandedH = (currentStyle?.GetEntryHeight() ?? 62) + CalcExpandedContentHeight(entry);
                            float entryBottom = entryTop + expandedH;
                            float visibleBottom = scrollTarget + contentRect.Height;
                            if (entryBottom > visibleBottom) {
                                scrollTarget += entryBottom - visibleBottom + 10f;
                                ClampScroll(panelRect);
                            }
                        }

                        SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.3f });
                    }

                    if (keyRightPressState == KeyPressState.Pressed) {
                        var entry = filteredEntries[idx];
                        if (ToggleEntryTracked(entry))
                            SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.4f });
                    }

                    bool middleDown = Mouse.GetState().MiddleButton == ButtonState.Pressed;
                    bool middleJustPressed = middleDown && !prevMiddleDown;
                    if (middleJustPressed) {
                        var entry = filteredEntries[idx];
                        if (ToggleEntrySuspended(entry))
                            SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.4f });
                    }
                }
            }
        }

        private bool ToggleEntryTracked(EntrustEntryData entry) {
            if (entry == null) return false;

            return entry.Status switch {
                QuestEntryStatus.Tracked => ChangeEntryStatus(entry, QuestEntryStatus.Active),
                QuestEntryStatus.Active => ChangeEntryStatus(entry, QuestEntryStatus.Tracked),
                QuestEntryStatus.Suspended => ChangeEntryStatus(entry, QuestEntryStatus.Tracked),
                _ => false,
            };
        }

        private bool ToggleEntrySuspended(EntrustEntryData entry) {
            if (entry == null) return false;

            return entry.Status switch {
                QuestEntryStatus.Suspended => ChangeEntryStatus(entry,
                    entry.RestoreTrackedOnUnsuspend ? QuestEntryStatus.Tracked : QuestEntryStatus.Active),
                QuestEntryStatus.Active or QuestEntryStatus.Tracked
                    => ChangeEntryStatus(entry, QuestEntryStatus.Suspended),
                _ => false,
            };
        }

        private bool ChangeEntryStatus(EntrustEntryData entry, QuestEntryStatus newStatus) {
            if (entry == null || entry.Status == newStatus) return false;

            QuestEntryStatus oldStatus = entry.Status;
            entry.RestoreTrackedOnUnsuspend = oldStatus == QuestEntryStatus.Tracked
                && newStatus == QuestEntryStatus.Suspended;
            entry.Status = newStatus;

            if (oldStatus == QuestEntryStatus.Suspended && newStatus != QuestEntryStatus.Suspended) {
                entry.RestoreTrackedOnUnsuspend = false;
                entry.OnUnsuspended?.Invoke();
            }

            entry.OnStatusChanged(oldStatus, newStatus);
            EmitStatusNotification(entry, oldStatus, newStatus);
            filterDirty = true;
            return true;
        }

        /// <summary>按状态变化发通知</summary>
        private static void EmitStatusNotification(EntrustEntryData entry,
            QuestEntryStatus oldStatus, QuestEntryStatus newStatus) {
            var kind = newStatus switch {
                QuestEntryStatus.Tracked when oldStatus == QuestEntryStatus.Suspended
                    => EntrustManagerNotification.NotifyKind.Unsuspended,
                QuestEntryStatus.Tracked => EntrustManagerNotification.NotifyKind.Tracked,
                QuestEntryStatus.Active when oldStatus == QuestEntryStatus.Tracked
                    => EntrustManagerNotification.NotifyKind.Untracked,
                QuestEntryStatus.Active when oldStatus == QuestEntryStatus.Suspended
                    => EntrustManagerNotification.NotifyKind.Unsuspended,
                QuestEntryStatus.Suspended => EntrustManagerNotification.NotifyKind.Suspended,
                QuestEntryStatus.Completed => EntrustManagerNotification.NotifyKind.Completed,
                _ => (EntrustManagerNotification.NotifyKind?)null,
            };
            if (kind.HasValue) {
                EntrustManagerNotification.Notify(entry.Title, kind.Value);
            }
        }

        /// <summary>按文字宽点选项卡，与 Draedon 一致</summary>
        private int GetTabIndexAtX(Rectangle tabRect, int mouseX) {
            var font = FontAssets.MouseText.Value;
            float scale = 0.72f;
            float tabX = tabRect.X + 6f;
            for (int i = 0; i < categoryNames.Length; i++) {
                float tabW = font.MeasureString(categoryNames[i]).X * scale + 18f;
                if (mouseX >= tabX && mouseX < tabX + tabW)
                    return i;
                tabX += tabW + 3f;
            }
            return -1;
        }

        private void ClampScroll(Rectangle panelRect) {
            Rectangle contentRect = GetContentRect(panelRect);
            float totalH = GetTotalEntriesHeight();
            float maxScroll = Math.Max(0f, totalH - contentRect.Height);
            scrollTarget = MathHelper.Clamp(scrollTarget, 0f, maxScroll);
        }

        /// <summary>条目动态高，含展开</summary>
        private int GetDynamicEntryHeight(EntrustEntryData entry) {
            int baseH = currentStyle?.GetEntryHeight() ?? 62;
            if (entry.ExpandProgress <= 0.001f) return baseH;

            int expandedContentH = CalcExpandedContentHeight(entry);
            int expandedH = baseH + expandedContentH;
            return (int)MathHelper.Lerp(baseH, expandedH, entry.ExpandProgress);
        }

        /// <summary>展开区额外高</summary>
        private int CalcExpandedContentHeight(EntrustEntryData entry) {
            string summary = entry.Summary ?? "";
            if (string.IsNullOrEmpty(summary)) return 0;

            var font = FontAssets.MouseText.Value;
            Rectangle contentRect = GetContentRect(hostRect);
            //展开区宽，对齐条目文本
            float textScale = 0.70f;
            int wrapPixelWidth = (int)((contentRect.Width - 50f) / textScale);

            //按\n拆段再换行
            int totalLineH = 0;
            string[] paragraphs = summary.Split('\n');
            foreach (string paragraph in paragraphs) {
                string trimmedPara = paragraph.Trim();
                if (string.IsNullOrEmpty(trimmedPara)) continue;
                string[] wrapped = VaultUtils.WrapTextArray(trimmedPara, font, wrapPixelWidth, 99, out _);
                foreach (string wl in wrapped) {
                    if (string.IsNullOrEmpty(wl)) continue;
                    totalLineH += (int)(font.MeasureString(wl.TrimEnd('-', ' ')).Y * textScale) + 2;
                }
            }
            //展开高=分隔6+行高+底边8+进度条行+提供者落款
            return 6 + totalLineH + 8 + BaseManagerStyle.ExpandedProgressRowH(entry)
                + (currentStyle?.GetProviderSignatureHeight(entry) ?? 0);
        }

        private float GetTotalEntriesHeight() {
            int padding = currentStyle?.GetEntryPadding() ?? 4;
            float total = 0f;
            foreach (var entry in filteredEntries) {
                total += GetDynamicEntryHeight(entry) + padding;
            }
            return total;
        }

        private float GetEntryYOffset(int targetIndex) {
            int padding = currentStyle?.GetEntryPadding() ?? 4;
            float y = 0f;
            for (int i = 0; i < targetIndex && i < filteredEntries.Count; i++) {
                y += GetDynamicEntryHeight(filteredEntries[i]) + padding;
            }
            return y;
        }

        private void RebuildFilteredEntries() {
            //重建过滤时折叠展开项
            foreach (var entry in allEntries) {
                entry.IsExpanded = false;
            }
            filteredEntries.Clear();

            IEnumerable<EntrustEntryData> source = allEntries;
            switch (selectedCategoryIndex) {
                case 0: // Active
                    source = allEntries.Where(e =>
                        e.Status == QuestEntryStatus.Active || e.Status == QuestEntryStatus.Tracked);
                    break;
                case 2: // Completed
                    source = allEntries.Where(e => e.Status == QuestEntryStatus.Completed);
                    break;
                case 3: // Suspended
                    source = allEntries.Where(e => e.Status == QuestEntryStatus.Suspended);
                    break;
            }

            //排序 Tracked>Active>…
            filteredEntries.AddRange(source.OrderBy(e => e.Status switch {
                QuestEntryStatus.Tracked => 0,
                QuestEntryStatus.Active => 1,
                QuestEntryStatus.Suspended => 2,
                QuestEntryStatus.Completed => 3,
                QuestEntryStatus.Failed => 4,
                _ => 5,
            }).ThenByDescending(e => e.Priority));
        }

        #endregion

        #region 矩形计算

        //内嵌后没有独立面板，页签带住在内容区顶部，页脚住在底部
        private static Rectangle GetTabRect(Rectangle panelRect)
            => new(panelRect.X, panelRect.Y, panelRect.Width, TabBarHeight);

        private static Rectangle GetContentRect(Rectangle panelRect) {
            int top = panelRect.Y + TabBarHeight;
            int bottom = panelRect.Bottom - FooterHeight;
            return new Rectangle(panelRect.X, top, panelRect.Width - ScrollbarWidth, bottom - top);
        }

        private static Rectangle GetScrollbarRect(Rectangle panelRect) {
            int top = panelRect.Y + TabBarHeight;
            int bottom = panelRect.Bottom - FooterHeight;
            return new Rectangle(panelRect.Right - ScrollbarWidth, top, ScrollbarWidth, bottom - top);
        }

        private static Rectangle GetFooterRect(Rectangle panelRect)
            => new(panelRect.X, panelRect.Bottom - FooterHeight, panelRect.Width, FooterHeight);

        #endregion

        #region 绘制

        //面板本体不再自绘，绘制全部走任务书调用的 DrawEmbedded
        public override void Draw(SpriteBatch spriteBatch) { }

        /// <summary>
        /// 内嵌绘制：页签带 → 条目 → 滚动指示 → 页脚 → 悬停提示。<br/>
        /// 底衬与外框归任务书的纸面，此处不再画面板背景与边框
        /// </summary>
        public void DrawEmbedded(SpriteBatch spriteBatch, Rectangle host, float alpha) {
            if (alpha <= 0.005f) {
                return;
            }
            hostRect = host;

            Rectangle tabRect = GetTabRect(host);
            currentStyle?.DrawCategoryTabs(spriteBatch, tabRect, categoryNames,
                selectedCategoryIndex, alpha);

            DrawQuestEntries(spriteBatch, host, alpha * MathF.Max(contentAlpha, 0.35f));

            DrawScrollbarArea(spriteBatch, host, alpha);

            Rectangle footerRect = GetFooterRect(host);
            int activeCount = 0;
            foreach (var e in allEntries) {
                if (e.Status == QuestEntryStatus.Active || e.Status == QuestEntryStatus.Tracked)
                    activeCount++;
            }
            currentStyle?.DrawFooter(spriteBatch, footerRect, allEntries.Count, activeCount, alpha);

            DrawInteractionHints(spriteBatch, host, alpha);
        }

        private void DrawQuestEntries(SpriteBatch sb, Rectangle panelRect, float alpha) {
            Rectangle contentRect = GetContentRect(panelRect);
            int padding = currentStyle?.GetEntryPadding() ?? 4;

            if (filteredEntries.Count == 0) {
                currentStyle?.DrawEmptyHint(sb, contentRect, EmptyHintText.Value, alpha);
                return;
            }

            //裁剪区，全宽含滚动条
            RasterizerState prevRasterizer = sb.GraphicsDevice.RasterizerState;
            Rectangle prevScissor = sb.GraphicsDevice.ScissorRectangle;

            sb.End();
            Rectangle clipRect = new(panelRect.X, contentRect.Y, panelRect.Width, contentRect.Height);
            Rectangle safeClip = Rectangle.Intersect(clipRect, sb.GraphicsDevice.Viewport.Bounds);
            sb.GraphicsDevice.ScissorRectangle = VaultUtils.GetClippingRectangle(sb, safeClip);
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.AnisotropicClamp,
                DepthStencilState.None, ScissorRaster, null, Main.UIScaleMatrix);

            float accumulatedY = 0f;
            for (int i = 0; i < filteredEntries.Count; i++) {
                int entryH = GetDynamicEntryHeight(filteredEntries[i]);
                float entryY = contentRect.Y + accumulatedY - scrollOffset;

                if (entryY + entryH < contentRect.Y - 10f) {
                    accumulatedY += entryH + padding;
                    continue;
                }
                if (entryY > contentRect.Bottom + 10f) break;

                Rectangle entryRect = new(contentRect.X, (int)entryY, contentRect.Width, entryH);
                bool isSelected = i == selectedIndex;
                bool isHovered = i == hoveredIndex;

                float entryAlpha = alpha;
                if (contentAlpha < 0.95f) {
                    float delay = i * 0.06f;
                    float denom = 1f - delay;
                    entryAlpha *= denom > 0.001f
                        ? MathHelper.Clamp((contentAlpha - delay) / denom, 0f, 1f)
                        : 0f;
                }

                currentStyle?.DrawQuestEntry(sb, entryRect, filteredEntries[i],
                    isSelected, isHovered, entryAlpha, i);

                if (i < filteredEntries.Count - 1) {
                    Vector2 sepStart = new(contentRect.X + 40f, entryY + entryH + padding / 2f);
                    Vector2 sepEnd = new(contentRect.Right - 12f, sepStart.Y);
                    currentStyle?.DrawEntrySeparator(sb, sepStart, sepEnd, alpha * 0.5f);
                }

                accumulatedY += entryH + padding;
            }

            sb.End();
            sb.GraphicsDevice.ScissorRectangle = prevScissor;
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.AnisotropicClamp,
                DepthStencilState.None, prevRasterizer, null, Main.UIScaleMatrix);
        }

        private void DrawScrollbarArea(SpriteBatch sb, Rectangle panelRect, float alpha) {
            Rectangle scrollRect = GetScrollbarRect(panelRect);
            Rectangle contentRect = GetContentRect(panelRect);

            float totalH = GetTotalEntriesHeight();
            if (totalH <= contentRect.Height) return; ; // 不需要滚动条

            float viewRatio = contentRect.Height / totalH;
            float scrollRatio = scrollOffset / Math.Max(1f, totalH - contentRect.Height);
            currentStyle?.DrawScrollbar(sb, scrollRect, scrollRatio, viewRatio, alpha);
        }

        private void DrawInteractionHints(SpriteBatch sb, Rectangle panelRect, float alpha) {
            if (hoveredIndex < 0 || hoveredIndex >= filteredEntries.Count) return;
            currentStyle?.DrawInteractionHints(sb, GetFooterRect(panelRect),
                filteredEntries[hoveredIndex], alpha);
        }

        #endregion

        #region 存档

        public override void SaveUIData(TagCompound tag) {
            tag[Name + ":selectedCategory"] = selectedCategoryIndex;
            tag[Name + ":styleIndex"] = currentStyleIndex;
        }

        public override void LoadUIData(TagCompound tag) {
            if (tag.TryGet(Name + ":selectedCategory", out int cat))
                selectedCategoryIndex = Math.Clamp(cat, 0, categoryKeys.Length - 1);
            if (tag.TryGet(Name + ":styleIndex", out int si)) {
                currentStyleIndex = Math.Clamp(si, 0, availableStyles.Count - 1);
                SetStyle(availableStyles[currentStyleIndex]);
            }
        }

        #endregion
    }
}

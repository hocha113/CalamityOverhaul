using CalamityOverhaul.Common;
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
using Terraria.GameInput;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.UI;

namespace CalamityOverhaul.Content.EntrustManager
{
    internal class QuestManagerSysteam : ModSystem
    {
        /// <summary>打开时隐藏的 Vanilla UI 层</summary>
        private static readonly HashSet<string> HiddenLayers = [
            "Vanilla: Hotbar",
            "Vanilla: Inventory",
            "Vanilla: Info Accessories Bar",
        ];

        public override void UpdateUI(GameTime gameTime) {
            if (CWRKeySystem.QuestManager_Key != null && CWRKeySystem.QuestManager_Key.JustReleased) {
                QuestManagerUI.Instance.TogglePanel();
            }
        }

        public override void OnWorldUnload() {
            QuestManagerUI.Instance?.ClearAll();
        }

        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers) {
            var ui = QuestManagerUI.Instance;
            if (ui == null || !ui.IsOpen) return;

            foreach (var layer in layers) {
                if (HiddenLayers.Contains(layer.Name)) {
                    layer.Active = false;
                }
            }
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
        public static LocalizedText OpenHintText { get; private set; }
        public static LocalizedText ExpandHintText { get; private set; }
        public static LocalizedText HeaderStatusTag { get; private set; }
        public static LocalizedText FooterStatsFormat { get; private set; }
        public static LocalizedText EntryStatusActive { get; private set; }
        public static LocalizedText EntryStatusTracked { get; private set; }
        public static LocalizedText EntryStatusSuspended { get; private set; }
        public static LocalizedText EntryStatusCompleted { get; private set; }
        public static LocalizedText EntryStatusFailed { get; private set; }

        public override void SetStaticDefaults() {
            TitleText = this.GetLocalization(nameof(TitleText), () => "任务管理");
            CategoryAll = this.GetLocalization(nameof(CategoryAll), () => "全部");
            CategoryActive = this.GetLocalization(nameof(CategoryActive), () => "进行中");
            CategoryCompleted = this.GetLocalization(nameof(CategoryCompleted), () => "已完成");
            CategorySuspended = this.GetLocalization(nameof(CategorySuspended), () => "挂起");
            EmptyHintText = this.GetLocalization(nameof(EmptyHintText), () => "暂无任务...");
            TrackHintText = this.GetLocalization(nameof(TrackHintText), () => "[右键] 关注/取消关注");
            SuspendHintText = this.GetLocalization(nameof(SuspendHintText), () => "[中键] 挂起/恢复");
            OpenHintText = this.GetLocalization(nameof(OpenHintText), () => "按 [L] 打开任务管理");
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

        private bool isOpen;

        public new bool IsOpen => isOpen;

        /// <summary>面板右缘 X，含滑入，供联动</summary>
        public int PanelRightEdge { get; private set; }

        /// <summary>开关动画 0~1</summary>
        private float openProgress;

        private float contentAlpha;

        private const int PanelWidth = 340;

        private const int PanelTopMargin = 30;

        private const int PanelBottomMargin = 30;

        private const int HeaderHeight = 38;

        private const int TabBarHeight = 28;

        private const int FooterHeight = 26;

        private const int ScrollbarWidth = 8;

        private const int CloseBtnSize = 20;

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

        private void CycleStyle() {
            if (availableStyles.Count <= 1) return;
            currentStyleIndex = (currentStyleIndex + 1) % availableStyles.Count;
            SetStyle(availableStyles[currentStyleIndex]);
            QuestLogs.QuestLog.Instance?.SetStyleByIndex(currentStyleIndex, false);
        }

        #endregion

        #region 动画

        private float panelShake;
        private float edgeGlowPhase;

        #endregion

        #region UIHandle 生命周期

        public override bool Active => !Main.gameMenu && (openProgress > 0.005f || isOpen || allEntries.Count > 0);

        public QuestManagerUI() {
            availableStyles.Add(new HotwindManagerStyle());
            availableStyles.Add(new DraedonManagerStyle());
            availableStyles.Add(new ForestManagerStyle());
            currentStyleIndex = 0;
            currentStyle = availableStyles[0];
            categoryNames = new string[4];
        }

        public override void OnEnterWorld() {
            isOpen = false;
            openProgress = 0f;
            contentAlpha = 0f;
            scrollOffset = 0f;
            scrollTarget = 0f;
            selectedIndex = -1;
            hoveredIndex = -1;
            selectedCategoryIndex = 0;
            panelShake = 0f;
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
            currentStyle?.Update(GetPanelRect(), openProgress);
        }

        public override void Update() {
            float targetOpen = isOpen ? 1f : 0f;
            openProgress = MathHelper.Lerp(openProgress, targetOpen, 0.12f);
            if (!isOpen && openProgress < 0.005f) openProgress = 0f;
            if (isOpen && openProgress > 0.995f) openProgress = 1f;

            float contentTarget = openProgress > 0.6f ? 1f : 0f;
            contentAlpha = MathHelper.Lerp(contentAlpha, contentTarget, 0.15f);

            edgeGlowPhase += 0.03f;
            if (edgeGlowPhase > MathHelper.TwoPi) edgeGlowPhase -= MathHelper.TwoPi;
            if (panelShake > 0f) panelShake *= 0.88f;

            scrollOffset = MathHelper.Lerp(scrollOffset, scrollTarget, 0.18f);

            if (filterDirty) {
                RebuildFilteredEntries();
                filterDirty = false;
            }

            foreach (var entry in allEntries) {
                entry.OnUpdate();
                entry.EntryStyle?.Update();

                float expandTarget = entry.IsExpanded ? 1f : 0f;
                entry.ExpandProgress = MathHelper.Lerp(entry.ExpandProgress, expandTarget, 0.14f);
                if (entry.ExpandProgress < 0.005f) entry.ExpandProgress = 0f;
                if (entry.ExpandProgress > 0.995f) entry.ExpandProgress = 1f;
            }

            Rectangle panelRect = GetPanelRect();
            PanelRightEdge = panelRect.Right;
            UIHitBox = panelRect;
            hoverInMainPage = panelRect.Intersects(MouseHitBox) && isOpen;

            if (!isOpen || openProgress < 0.3f) return;

            if (Main.playerInventory) {
                isOpen = false;
                SoundEngine.PlaySound(SoundID.MenuClose with { Volume = 0.5f });
                return;
            }

            UIInputGuard.SuppressWeaponSwitch();

            if (hoverInMainPage) {
                player.mouseInterface = true;
                HandleScrollInput(panelRect);
                HandleMouseInput(panelRect);
            }

            //中键态每帧更新，防跨帧漂移
            prevMiddleDown = Mouse.GetState().MiddleButton == ButtonState.Pressed;
        }

        #endregion

        #region 开关与交互

        public void TogglePanel() {
            isOpen = !isOpen;
            if (isOpen) {
                //关背包防遮挡
                Main.playerInventory = false;
                panelShake = 3f;
                SoundEngine.PlaySound(SoundID.MenuOpen with { Volume = 0.5f });
            }
            else {
                SoundEngine.PlaySound(SoundID.MenuClose with { Volume = 0.5f });
            }
        }

        private void HandleScrollInput(Rectangle panelRect) {
            int scrollDelta = PlayerInput.ScrollWheelDeltaForUI;
            if (scrollDelta != 0) {
                scrollTarget -= scrollDelta * 0.3f;
                ClampScroll(panelRect);
            }
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

            if (currentStyle != null) {
                Rectangle styleRect = currentStyle.GetStyleSwitchButtonRect(panelRect);
                if (styleRect.Contains(Main.mouseX, Main.mouseY)) {
                    if (keyLeftPressState == KeyPressState.Pressed) {
                        CycleStyle();
                        SoundEngine.PlaySound(SoundID.MenuTick);
                    }
                    return;
                }
            }

            Rectangle closeBtnRect = GetCloseButtonRect(panelRect);
            if (closeBtnRect.Contains(Main.mouseX, Main.mouseY) && keyLeftPressState == KeyPressState.Pressed) {
                TogglePanel();
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
            Rectangle panelRect = GetPanelRect();
            Rectangle contentRect = GetContentRect(panelRect);
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
            //展开高=分隔6+行高+底边8
            return 6 + totalLineH + 8;
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

        private Rectangle GetPanelRect() {
            int panelH = Main.screenHeight - PanelTopMargin - PanelBottomMargin;
            float eased = VaultUtils.EaseOutCubic(MathHelper.Clamp(openProgress, 0f, 1f));
            int panelX = (int)MathHelper.Lerp(-PanelWidth - 20f, 0f, eased);

            if (panelShake > 0.1f) {
                panelX += (int)(MathF.Sin(edgeGlowPhase * 12f) * panelShake);
            }

            return new Rectangle(panelX, PanelTopMargin, PanelWidth, panelH);
        }

        private Rectangle GetHeaderRect(Rectangle panelRect) {
            return new Rectangle(panelRect.X, panelRect.Y, panelRect.Width, HeaderHeight);
        }

        private Rectangle GetTabRect(Rectangle panelRect) {
            return new Rectangle(panelRect.X, panelRect.Y + HeaderHeight, panelRect.Width, TabBarHeight);
        }

        private Rectangle GetContentRect(Rectangle panelRect) {
            int top = panelRect.Y + HeaderHeight + TabBarHeight;
            int bottom = panelRect.Bottom - FooterHeight;
            return new Rectangle(panelRect.X, top, panelRect.Width - ScrollbarWidth, bottom - top);
        }

        private Rectangle GetScrollbarRect(Rectangle panelRect) {
            int top = panelRect.Y + HeaderHeight + TabBarHeight;
            int bottom = panelRect.Bottom - FooterHeight;
            return new Rectangle(panelRect.Right - ScrollbarWidth, top, ScrollbarWidth, bottom - top);
        }

        private Rectangle GetFooterRect(Rectangle panelRect) {
            return new Rectangle(panelRect.X, panelRect.Bottom - FooterHeight, panelRect.Width, FooterHeight);
        }

        private Rectangle GetCloseButtonRect(Rectangle panelRect) {
            return new Rectangle(panelRect.Right - CloseBtnSize - 8, panelRect.Y + (HeaderHeight - CloseBtnSize) / 2,
                CloseBtnSize, CloseBtnSize);
        }

        #endregion

        #region 绘制

        public override void Draw(SpriteBatch spriteBatch) {
            if (openProgress <= 0.005f) return;

            Rectangle panelRect = GetPanelRect();
            float alpha = openProgress;

            currentStyle?.DrawPanelBackground(spriteBatch, panelRect, alpha);

            currentStyle?.DrawParticles(spriteBatch, panelRect, alpha);

            currentStyle?.DrawPanelFrame(spriteBatch, panelRect, alpha);

            Rectangle headerRect = GetHeaderRect(panelRect);
            currentStyle?.DrawHeader(spriteBatch, headerRect, TitleText.Value, alpha);

            DrawCloseButton(spriteBatch, panelRect, alpha);

            if (currentStyle != null && availableStyles.Count > 1) {
                Rectangle styleRect = currentStyle.GetStyleSwitchButtonRect(panelRect);
                bool styleHovered = styleRect.Contains(Main.mouseX, Main.mouseY) && isOpen;
                currentStyle.DrawStyleSwitchButton(spriteBatch, panelRect, styleHovered, alpha);
            }

            if (contentAlpha < 0.01f) {
                DrawLoadingIndicator(spriteBatch, panelRect, alpha);
                currentStyle?.DrawOverlayEffects(spriteBatch, panelRect, alpha);
                return;
            }

            Rectangle tabRect = GetTabRect(panelRect);
            currentStyle?.DrawCategoryTabs(spriteBatch, tabRect, categoryNames,
                selectedCategoryIndex, alpha * contentAlpha);

            DrawQuestEntries(spriteBatch, panelRect, alpha * contentAlpha);

            DrawScrollbarArea(spriteBatch, panelRect, alpha * contentAlpha);

            Rectangle footerRect = GetFooterRect(panelRect);
            int activeCount = 0;
            foreach (var e in allEntries) {
                if (e.Status == QuestEntryStatus.Active || e.Status == QuestEntryStatus.Tracked)
                    activeCount++;
            }
            currentStyle?.DrawFooter(spriteBatch, footerRect, allEntries.Count, activeCount, alpha * contentAlpha);

            currentStyle?.DrawOverlayEffects(spriteBatch, panelRect, alpha);

            DrawInteractionHints(spriteBatch, panelRect, alpha * contentAlpha);
        }

        private void DrawCloseButton(SpriteBatch sb, Rectangle panelRect, float alpha) {
            Rectangle btn = GetCloseButtonRect(panelRect);
            bool hovered = btn.Contains(Main.mouseX, Main.mouseY) && isOpen;

            Color bgC = hovered ? new Color(60, 150, 220) * (alpha * 0.3f) : new Color(10, 20, 40) * (alpha * 0.4f);
            BaseManagerStyle.FillRect(sb, btn, bgC);

            Color xColor = hovered ? new Color(255, 100, 100) * alpha : new Color(140, 210, 255) * (alpha * 0.6f);
            float cx = btn.X + btn.Width / 2f;
            float cy = btn.Y + btn.Height / 2f;
            float xSize = 4f;
            sb.Draw(VaultAsset.placeholder2.Value, new Vector2(cx, cy), null, xColor,
                MathHelper.PiOver4, new Vector2(0.5f), new Vector2(xSize * 2f, 1.5f), SpriteEffects.None, 0f);
            sb.Draw(VaultAsset.placeholder2.Value, new Vector2(cx, cy), null, xColor,
                -MathHelper.PiOver4, new Vector2(0.5f), new Vector2(xSize * 2f, 1.5f), SpriteEffects.None, 0f);
        }

        private void DrawLoadingIndicator(SpriteBatch sb, Rectangle panelRect, float alpha) {
            float t = openProgress * 8f;
            string dots = "";
            for (int i = 0; i < 3; i++) {
                float phase = MathF.Sin(t + i * 0.8f);
                dots += phase > 0f ? "●" : "○";
                if (i < 2) dots += " ";
            }
            Vector2 center = new(panelRect.X + panelRect.Width / 2f, panelRect.Y + panelRect.Height / 2f);
            BaseManagerStyle.DrawCenteredText(sb, dots, center, new Color(140, 210, 255) * (alpha * 0.5f), 0.8f);
        }

        private void DrawQuestEntries(SpriteBatch sb, Rectangle panelRect, float alpha) {
            Rectangle contentRect = GetContentRect(panelRect);
            int padding = currentStyle?.GetEntryPadding() ?? 4;

            if (filteredEntries.Count == 0) {
                Vector2 emptyCenter = new(contentRect.X + contentRect.Width / 2f,
                    contentRect.Y + contentRect.Height / 2f);
                BaseManagerStyle.DrawCenteredText(sb, EmptyHintText.Value, emptyCenter,
                    new Color(60, 150, 220) * (alpha * 0.4f), 0.75f);
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

            Rectangle footerRect = GetFooterRect(panelRect);
            var entry = filteredEntries[hoveredIndex];
            var font = FontAssets.MouseText.Value;

            float hintY = footerRect.Y - 16f;

            string suspendHint = "";
            if (entry.Status == QuestEntryStatus.Active || entry.Status == QuestEntryStatus.Tracked
                || entry.Status == QuestEntryStatus.Suspended)
                suspendHint = SuspendHintText.Value;

            if (!string.IsNullOrEmpty(suspendHint)) {
                float suspendW = font.MeasureString(suspendHint).X * 0.55f;
                Utils.DrawBorderString(sb, suspendHint,
                    new Vector2(footerRect.Right - suspendW - 10f, hintY),
                    new Color(200, 180, 100) * (alpha * 0.5f), 0.55f);
                hintY -= 14f;
            }

            string trackHint = "";
            if (entry.Status == QuestEntryStatus.Active || entry.Status == QuestEntryStatus.Tracked)
                trackHint = TrackHintText.Value;

            if (!string.IsNullOrEmpty(trackHint)) {
                float hintW = font.MeasureString(trackHint).X * 0.55f;
                Utils.DrawBorderString(sb, trackHint,
                    new Vector2(footerRect.Right - hintW - 10f, hintY),
                    new Color(140, 210, 255) * (alpha * 0.5f), 0.55f);
                hintY -= 14f;
            }

            string expandHint = ExpandHintText.Value;
            if (!string.IsNullOrEmpty(expandHint)) {
                float expandW = font.MeasureString(expandHint).X * 0.55f;
                Utils.DrawBorderString(sb, expandHint,
                    new Vector2(footerRect.Right - expandW - 10f, hintY),
                    new Color(120, 200, 180) * (alpha * 0.5f), 0.55f);
            }
        }

        #endregion

        #region 存档

        public override void SaveUIData(TagCompound tag) {
            tag[Name + ":isOpen"] = isOpen;
            tag[Name + ":selectedCategory"] = selectedCategoryIndex;
            tag[Name + ":styleIndex"] = currentStyleIndex;
        }

        public override void LoadUIData(TagCompound tag) {
            if (tag.TryGet(Name + ":isOpen", out bool open))
                isOpen = open;
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

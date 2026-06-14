using CalamityOverhaul.Common;
using CalamityOverhaul.Content.ADV.EntrustManager.Styles;
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

namespace CalamityOverhaul.Content.ADV.EntrustManager
{
    internal class QuestManagerSysteam : ModSystem
    {
        /// <summary>需要在委托管理界面打开时隐藏的 Vanilla UI 层</summary>
        private static readonly HashSet<string> HiddenLayers = [
            "Vanilla: Hotbar",
            "Vanilla: Inventory",
            "Vanilla: Info Accessories Bar",
        ];

        public override void UpdateUI(GameTime gameTime) {
            //快捷键开关
            if (CWRKeySystem.QuestManager_Key != null && CWRKeySystem.QuestManager_Key.JustReleased) {
                QuestManagerUI.Instance.TogglePanel();
            }
        }

        public override void OnWorldUnload() {
            //换存档清空委托列表
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

        /// <summary>面板是否打开</summary>
        private bool isOpen;

        /// <summary>外部只读访问</summary>
        public new bool IsOpen => isOpen;

        /// <summary>当前面板右边缘X坐标（含滑动动画），用于其他UI联动定位</summary>
        public int PanelRightEdge { get; private set; }

        /// <summary>打开/关闭动画进度 0~1</summary>
        private float openProgress;

        /// <summary>内容淡入进度</summary>
        private float contentAlpha;

        /// <summary>面板宽度</summary>
        private const int PanelWidth = 340;

        /// <summary>面板上边距</summary>
        private const int PanelTopMargin = 30;

        /// <summary>面板下边距</summary>
        private const int PanelBottomMargin = 30;

        /// <summary>标题栏高度</summary>
        private const int HeaderHeight = 38;

        /// <summary>选项卡栏高度</summary>
        private const int TabBarHeight = 28;

        /// <summary>底部状态栏高度</summary>
        private const int FooterHeight = 26;

        /// <summary>滚动条宽度</summary>
        private const int ScrollbarWidth = 8;

        /// <summary>关闭按钮尺寸</summary>
        private const int CloseBtnSize = 20;

        #endregion

        #region 滚动与选中

        private float scrollOffset;
        private float scrollTarget;
        private int selectedIndex = -1;
        private int hoveredIndex = -1;
        private int selectedCategoryIndex;
        /// <summary>过滤列表是否需要重建</summary>
        private bool filterDirty = true;
        /// <summary>上一帧鼠标中键是否按下，用于检测中键点击</summary>
        private bool prevMiddleDown;

        private readonly string[] categoryKeys = ["Active", "All", "Completed", "Suspended"];
        private string[] categoryNames;

        private static readonly RasterizerState ScissorRaster = new() { ScissorTestEnable = true };

        #endregion

        #region 任务数据

        /// <summary>所有已注册条目</summary>
        private readonly List<EntrustEntryData> allEntries = [];

        /// <summary>当前过滤后的显示列表</summary>
        private readonly List<EntrustEntryData> filteredEntries = [];

        /// <summary>注册一条任务到管理器</summary>
        public void RegisterQuest(EntrustEntryData entry) {
            if (allEntries.All(e => e.Key != entry.Key)) {
                //新注册 Active 自动设为关注
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

        /// <summary>移除一条任务</summary>
        public void UnregisterQuest(string key) {
            if (allEntries.RemoveAll(e => e.Key == key) > 0)
                filterDirty = true;
        }

        /// <summary>根据 key 获取任务条目</summary>
        public EntrustEntryData GetEntry(string key) {
            return allEntries.Find(e => e.Key == key);
        }

        /// <summary>清空所有任务</summary>
        public void ClearAll() {
            allEntries.Clear();
            filteredEntries.Clear();
            selectedIndex = -1;
            hoveredIndex = -1;
            scrollOffset = 0f;
            scrollTarget = 0f;
            filterDirty = true;
        }

        /// <summary>标记过滤列表需要重建</summary>
        public void MarkFilterDirty() => filterDirty = true;

        /// <summary>集中修改条目状态，触发通知与过滤刷新</summary>
        public bool SetEntryStatus(string key, QuestEntryStatus newStatus, float? progress = null) {
            var entry = GetEntry(key);
            if (entry == null || entry.Status == newStatus) return false;

            if (progress.HasValue) entry.Progress = progress.Value;
            return ChangeEntryStatus(entry, newStatus);
        }

        /// <summary>获取所有被关注状态的条目，供 <see cref="EntrustTrackerWidget"/> 查询</summary>
        public void GetTrackedEntries(List<EntrustEntryData> result) {
            foreach (var e in allEntries) {
                if (e.Status == QuestEntryStatus.Tracked)
                    result.Add(e);
            }
        }

        /// <summary>是否存在任意已注册的委托条目</summary>
        public bool HasAnyEntry => allEntries.Count > 0;

        /// <summary>是否存在被关注条目</summary>
        public bool HasTrackedEntries() {
            foreach (var e in allEntries) {
                if (e.Status == QuestEntryStatus.Tracked)
                    return true;
            }
            return false;
        }

        /// <summary>统计指定状态条目数，引导兜底用</summary>
        public int CountByStatus(QuestEntryStatus status) {
            int n = 0;
            foreach (var e in allEntries) {
                if (e.Status == status) n++;
            }
            return n;
        }

        /// <summary>取出第一条可被兜底自动关注的条目Key（优先 Active，其次 Suspended）</summary>
        public string TryGetFirstTrackableKey() {
            foreach (var e in allEntries) {
                if (e.Status == QuestEntryStatus.Active) return e.Key;
            }
            foreach (var e in allEntries) {
                if (e.Status == QuestEntryStatus.Suspended) return e.Key;
            }
            return null;
        }

        /// <summary>取出第一条可被兜底自动挂起的条目Key（优先非追踪的 Active）</summary>
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

        /// <summary>切换样式</summary>
        public void SetStyle(IEntrustManagerStyle style) {
            currentStyle?.Reset();
            currentStyle = style;
        }

        /// <summary>按索引设置样式，sync为true时同步任务书样式</summary>
        public void SetStyleByIndex(int index, bool sync = true) {
            if (availableStyles.Count == 0) return;
            currentStyleIndex = Math.Clamp(index, 0, availableStyles.Count - 1);
            SetStyle(availableStyles[currentStyleIndex]);
            if (sync) {
                QuestLogs.QuestLog.Instance?.SetStyleByIndex(currentStyleIndex, false);
            }
        }

        /// <summary>切换到下一个可用样式</summary>
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

            //条目由各 QuestLine ModSystem 注册
        }

        public override void LogicUpdate() {
            currentStyle?.Update(GetPanelRect(), openProgress);
        }

        public override void Update() {
            //动画插值
            float targetOpen = isOpen ? 1f : 0f;
            openProgress = MathHelper.Lerp(openProgress, targetOpen, 0.12f);
            if (!isOpen && openProgress < 0.005f) openProgress = 0f;
            if (isOpen && openProgress > 0.995f) openProgress = 1f;

            //内容淡入延迟
            float contentTarget = openProgress > 0.6f ? 1f : 0f;
            contentAlpha = MathHelper.Lerp(contentAlpha, contentTarget, 0.15f);

            //动画计时器
            edgeGlowPhase += 0.03f;
            if (edgeGlowPhase > MathHelper.TwoPi) edgeGlowPhase -= MathHelper.TwoPi;
            if (panelShake > 0f) panelShake *= 0.88f;

            //滚动平滑
            scrollOffset = MathHelper.Lerp(scrollOffset, scrollTarget, 0.18f);

            //按需刷新过滤列表
            if (filterDirty) {
                RebuildFilteredEntries();
                filterDirty = false;
            }

            //条目实时数据与样式
            foreach (var entry in allEntries) {
                entry.OnUpdate();
                entry.EntryStyle?.Update();

                //展开/折叠动画插值
                float expandTarget = entry.IsExpanded ? 1f : 0f;
                entry.ExpandProgress = MathHelper.Lerp(entry.ExpandProgress, expandTarget, 0.14f);
                if (entry.ExpandProgress < 0.005f) entry.ExpandProgress = 0f;
                if (entry.ExpandProgress > 0.995f) entry.ExpandProgress = 1f;
            }

            //面板碰撞区域
            Rectangle panelRect = GetPanelRect();
            PanelRightEdge = panelRect.Right;
            UIHitBox = panelRect;
            hoverInMainPage = panelRect.Intersects(MouseHitBox) && isOpen;

            if (!isOpen || openProgress < 0.3f) return;

            //打开背包时收起面板
            if (Main.playerInventory) {
                isOpen = false;
                SoundEngine.PlaySound(SoundID.MenuClose with { Volume = 0.5f });
                return;
            }

            player.CWR().DontSwitchWeaponTime = 2;

            //交互处理
            if (hoverInMainPage) {
                player.mouseInterface = true;
                HandleScrollInput(panelRect);
                HandleMouseInput(panelRect);
            }

            //始终更新中键状态，避免跨帧漂移
            prevMiddleDown = Mouse.GetState().MiddleButton == ButtonState.Pressed;
        }

        #endregion

        #region 开关与交互

        /// <summary>切换面板开关状态</summary>
        public void TogglePanel() {
            isOpen = !isOpen;
            if (isOpen) {
                //关闭背包，避免遮挡
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

            //分类选项卡点击（按实际文字宽度匹配）
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

            //样式切换按钮
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

            //关闭按钮
            Rectangle closeBtnRect = GetCloseButtonRect(panelRect);
            if (closeBtnRect.Contains(Main.mouseX, Main.mouseY) && keyLeftPressState == KeyPressState.Pressed) {
                TogglePanel();
                return;
            }

            //任务条目交互
            hoveredIndex = -1;
            if (contentRect.Contains(Main.mouseX, Main.mouseY)) {
                float relativeY = Main.mouseY - contentRect.Y + scrollOffset;
                //遍历累积高度来确定悬停的条目索引
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

                    //左键展开/折叠
                    if (keyLeftPressState == KeyPressState.Pressed) {
                        var entry = filteredEntries[idx];
                        //折叠其他已展开的条目
                        foreach (var other in filteredEntries) {
                            if (other != entry && other.IsExpanded)
                                other.IsExpanded = false;
                        }
                        entry.IsExpanded = !entry.IsExpanded;
                        selectedIndex = entry.IsExpanded ? idx : -1;

                        //展开时自动滚动确保展开内容可见
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

                    //右键关注/取消关注
                    if (keyRightPressState == KeyPressState.Pressed) {
                        var entry = filteredEntries[idx];
                        if (ToggleEntryTracked(entry))
                            SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.4f });
                    }

                    //中键挂起/恢复
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

        /// <summary>根据状态变化发射对应的通知弹窗</summary>
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

        /// <summary>根据渲染时文字宽度匹配选项卡索引（与 DraedonManagerStyle.DrawCategoryTabs 一致）</summary>
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

        /// <summary>条目动态高度含展开区</summary>
        private int GetDynamicEntryHeight(EntrustEntryData entry) {
            int baseH = currentStyle?.GetEntryHeight() ?? 62;
            if (entry.ExpandProgress <= 0.001f) return baseH;

            int expandedContentH = CalcExpandedContentHeight(entry);
            int expandedH = baseH + expandedContentH;
            return (int)MathHelper.Lerp(baseH, expandedH, entry.ExpandProgress);
        }

        /// <summary>展开描述区额外高度</summary>
        private int CalcExpandedContentHeight(EntrustEntryData entry) {
            string summary = entry.Summary ?? "";
            if (string.IsNullOrEmpty(summary)) return 0;

            var font = FontAssets.MouseText.Value;
            Rectangle panelRect = GetPanelRect();
            Rectangle contentRect = GetContentRect(panelRect);
            //展开区域的可用宽度（与条目内文本对齐，减去左侧偏移）
            float textScale = 0.70f;
            int wrapPixelWidth = (int)((contentRect.Width - 50f) / textScale);

            //按换行符拆分后逐段换行，与绘制逻辑一致
            int totalLineH = 0;
            string[] paragraphs = summary.Split('\n');
            foreach (string paragraph in paragraphs) {
                string trimmedPara = paragraph.Trim();
                if (string.IsNullOrEmpty(trimmedPara)) continue;
                string[] wrapped = CWRUtils.WrapTextArray(trimmedPara, font, wrapPixelWidth, 99, out _);
                foreach (string wl in wrapped) {
                    if (string.IsNullOrEmpty(wl)) continue;
                    totalLineH += (int)(font.MeasureString(wl.TrimEnd('-', ' ')).Y * textScale) + 2;
                }
            }
            //展开区域 = 分隔线(6) + 文本行高 + 底部边距(8)
            return 6 + totalLineH + 8;
        }

        /// <summary>获取过滤列表中所有条目的动态高度总和（含间距）</summary>
        private float GetTotalEntriesHeight() {
            int padding = currentStyle?.GetEntryPadding() ?? 4;
            float total = 0f;
            foreach (var entry in filteredEntries) {
                total += GetDynamicEntryHeight(entry) + padding;
            }
            return total;
        }

        /// <summary>获取指定过滤列表索引处条目的Y偏移（相对于内容区顶部）</summary>
        private float GetEntryYOffset(int targetIndex) {
            int padding = currentStyle?.GetEntryPadding() ?? 4;
            float y = 0f;
            for (int i = 0; i < targetIndex && i < filteredEntries.Count; i++) {
                y += GetDynamicEntryHeight(filteredEntries[i]) + padding;
            }
            return y;
        }

        private void RebuildFilteredEntries() {
            //重建过滤列表时折叠所有展开的条目
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

            //排序：Tracked > Active > Suspended > Completed > Failed
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

            //打开/关闭时的轻微抖动
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

            //1 面板背景
            currentStyle?.DrawPanelBackground(spriteBatch, panelRect, alpha);

            //2 背景粒子
            currentStyle?.DrawParticles(spriteBatch, panelRect, alpha);

            //3 面板边框
            currentStyle?.DrawPanelFrame(spriteBatch, panelRect, alpha);

            //4 标题栏
            Rectangle headerRect = GetHeaderRect(panelRect);
            currentStyle?.DrawHeader(spriteBatch, headerRect, TitleText.Value, alpha);

            //5 关闭按钮
            DrawCloseButton(spriteBatch, panelRect, alpha);

            //5.5 样式切换
            if (currentStyle != null && availableStyles.Count > 1) {
                Rectangle styleRect = currentStyle.GetStyleSwitchButtonRect(panelRect);
                bool styleHovered = styleRect.Contains(Main.mouseX, Main.mouseY) && isOpen;
                currentStyle.DrawStyleSwitchButton(spriteBatch, panelRect, styleHovered, alpha);
            }

            if (contentAlpha < 0.01f) {
                //展开中加载指示
                DrawLoadingIndicator(spriteBatch, panelRect, alpha);
                currentStyle?.DrawOverlayEffects(spriteBatch, panelRect, alpha);
                return;
            }

            //6 分类选项卡
            Rectangle tabRect = GetTabRect(panelRect);
            currentStyle?.DrawCategoryTabs(spriteBatch, tabRect, categoryNames,
                selectedCategoryIndex, alpha * contentAlpha);

            //7 条目列表
            DrawQuestEntries(spriteBatch, panelRect, alpha * contentAlpha);

            //8 滚动条
            DrawScrollbarArea(spriteBatch, panelRect, alpha * contentAlpha);

            //9 底部状态栏
            Rectangle footerRect = GetFooterRect(panelRect);
            int activeCount = 0;
            foreach (var e in allEntries) {
                if (e.Status == QuestEntryStatus.Active || e.Status == QuestEntryStatus.Tracked)
                    activeCount++;
            }
            currentStyle?.DrawFooter(spriteBatch, footerRect, allEntries.Count, activeCount, alpha * contentAlpha);

            //10 前景特效
            currentStyle?.DrawOverlayEffects(spriteBatch, panelRect, alpha);

            //11 操作提示
            DrawInteractionHints(spriteBatch, panelRect, alpha * contentAlpha);
        }

        private void DrawCloseButton(SpriteBatch sb, Rectangle panelRect, float alpha) {
            Rectangle btn = GetCloseButtonRect(panelRect);
            bool hovered = btn.Contains(Main.mouseX, Main.mouseY) && isOpen;

            //按钮背景
            Color bgC = hovered ? new Color(60, 150, 220) * (alpha * 0.3f) : new Color(10, 20, 40) * (alpha * 0.4f);
            BaseManagerStyle.FillRect(sb, btn, bgC);

            //X 标记
            Color xColor = hovered ? new Color(255, 100, 100) * alpha : new Color(140, 210, 255) * (alpha * 0.6f);
            float cx = btn.X + btn.Width / 2f;
            float cy = btn.Y + btn.Height / 2f;
            float xSize = 4f;
            //两条交叉线
            sb.Draw(VaultAsset.placeholder2.Value, new Vector2(cx, cy), null, xColor,
                MathHelper.PiOver4, new Vector2(0.5f), new Vector2(xSize * 2f, 1.5f), SpriteEffects.None, 0f);
            sb.Draw(VaultAsset.placeholder2.Value, new Vector2(cx, cy), null, xColor,
                -MathHelper.PiOver4, new Vector2(0.5f), new Vector2(xSize * 2f, 1.5f), SpriteEffects.None, 0f);
        }

        private void DrawLoadingIndicator(SpriteBatch sb, Rectangle panelRect, float alpha) {
            //展开加载动画三点闪烁
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
                //空列表提示
                Vector2 emptyCenter = new(contentRect.X + contentRect.Width / 2f,
                    contentRect.Y + contentRect.Height / 2f);
                BaseManagerStyle.DrawCenteredText(sb, EmptyHintText.Value, emptyCenter,
                    new Color(60, 150, 220) * (alpha * 0.4f), 0.75f);
                return;
            }

            //裁剪区域，面板全宽含滚动条
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

                //视锥裁剪
                if (entryY + entryH < contentRect.Y - 10f) {
                    accumulatedY += entryH + padding;
                    continue;
                }
                if (entryY > contentRect.Bottom + 10f) break;

                Rectangle entryRect = new(contentRect.X, (int)entryY, contentRect.Width, entryH);
                bool isSelected = i == selectedIndex;
                bool isHovered = i == hoveredIndex;

                //入场动画（依次延迟淡入）
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

                //条目分隔线
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

            //在面板底部状态栏上方显示操作提示
            Rectangle footerRect = GetFooterRect(panelRect);
            var entry = filteredEntries[hoveredIndex];
            var font = FontAssets.MouseText.Value;

            float hintY = footerRect.Y - 16f;

            //挂起/恢复提示（所有非完成、非失败状态均可操作）
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

            //关注/取消关注提示
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

            //展开/折叠提示
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

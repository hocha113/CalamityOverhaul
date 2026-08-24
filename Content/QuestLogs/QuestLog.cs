using CalamityOverhaul.Common;
using CalamityOverhaul.Content.EntrustManager;
using CalamityOverhaul.Content.QuestLogs.Core;
using CalamityOverhaul.Content.QuestLogs.Guide;
using CalamityOverhaul.Content.QuestLogs.Styles;
using CalamityOverhaul.Content.QuestLogs.Styles.Chronicle;
using CalamityOverhaul.Content.TimeFreezes;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.UI;

namespace CalamityOverhaul.Content.QuestLogs
{
    /// <summary>任务书当前摊开的站点</summary>
    public enum QuestLogView
    {
        /// <summary>任务图谱</summary>
        Chart,
        /// <summary>委托卷宗</summary>
        Entrust,
    }

    /// <summary>任务书全屏摊开期间隐藏的原版 UI 层</summary>
    internal class QuestLogInterfaceSystem : ModSystem
    {
        private static readonly HashSet<string> HiddenLayers = [
            "Vanilla: Hotbar",
            "Vanilla: Resource Bars",
            "Vanilla: Inventory",
            "Vanilla: Info Accessories Bar",
            "Vanilla: Map / Minimap",
            "Vanilla: Entity Health Bars",
            "Vanilla: Emote Bubbles",
            "Vanilla: Builder Accessories",
            "Vanilla: Radial Hotbars",
        ];

        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers) {
            QuestLog log = QuestLog.Instance;
            //含合书过程，否则淡出期原版 HUD 会先弹回来
            if (log == null || (!log.IsOpen && log.OpenProgress.Current < 0.5f)) {
                return;
            }
            foreach (var layer in layers) {
                if (HiddenLayers.Contains(layer.Name)) {
                    layer.Active = false;
                }
            }
        }
    }

    public class QuestLog : UIHandle, ILocalizedModType
    {
        [VaultLoaden(CWRConstant.UI)]
        public static Asset<Texture2D> QuestLogStart = null;
        public static QuestLog Instance => UIHandleLoader.GetUIHandleOfType<QuestLog>();

        //WorldFreezeSystem 的 reason 标签，单人开书即冻世界
        private const string FreezeReason = "QuestLog";

        /// <summary>左栏站点数：图谱、委托卷宗恒两站</summary>
        public int StationCount => 2;

        /// <summary>第 index 个站点对应的视图</summary>
        public QuestLogView StationAt(int index)
            => index == 0 ? QuestLogView.Chart : QuestLogView.Entrust;

        //摊开期间恒活跃；背包里的启动图标同样恒显示
        public override bool Active
            => IsOpen || OpenProgress.Current > 0.001f || Main.playerInventory;

        //详情栏摊开时先退详情，Esc 的第二下才合书
        public override bool CloseOnEscape => !showDetailPanel;

        public override SoundStyle? OpenSound => CWRSound.ButtonZero with { Pitch = 0.1f, Volume = 0.6f };

        public override SoundStyle? CloseSound => CWRSound.ButtonZero with { Pitch = -0.1f, Volume = 0.6f };

        public float MainPanelAlpha => mainPanelAlpha;
        private float mainPanelAlpha;

        public IQuestLogStyle CurrentStyle { get; set; } = new HotwindQuestLogStyle();

        public IReadOnlyCollection<QuestNode> Nodes => QuestNode.AllQuests;

        /// <summary>本帧的全屏分区</summary>
        public QuestLogLayout CurrentLayout => layout;
        private QuestLogLayout layout;

        /// <summary>当前摊开的站点</summary>
        public QuestLogView View { get; private set; } = QuestLogView.Chart;

        /// <summary>委托卷宗是否正摊在书里，供委托管理器判定内嵌态</summary>
        public bool EntrustViewActive => IsOpen && View == QuestLogView.Entrust;

        /// <summary>内嵌内容区（委托卷宗铺在这里）</summary>
        public Rectangle ContentHostRect => layout.Canvas;

        /// <summary>章目：没有前置的根节点，左栏据此列目并跳转</summary>
        public IReadOnlyList<QuestNode> ChapterRoots => chapterRoots;
        private readonly List<QuestNode> chapterRoots = [];

        #region 教程只读态

        //教程要判断玩家是否真的动过视图、点开过节点，读书自己的字段，别另存一份

        /// <summary>图谱缩放系数</summary>
        public float ChartZoom => zoom;

        /// <summary>图谱平移量</summary>
        public Vector2 ChartPan => panOffset;

        /// <summary>右侧详情栏是否摊开</summary>
        public bool DetailOpen => showDetailPanel;

        /// <summary>图谱上有没有节点可讲，没有就别对着空画布讲解</summary>
        public bool HasChartNodes => Nodes.Count > 0;

        /// <summary>
        /// 目标节点此刻的屏幕矩形，随缩放平移实时变化；节点滚出画布时夹回画布内缘，
        /// 让圈定环贴边指向它的方向。书不在图谱站点或节点缺席时返回 false
        /// </summary>
        public bool TryGetNodeGuideRect(QuestNode node, out Rectangle rect) {
            rect = Rectangle.Empty;
            if (node == null || View != QuestLogView.Chart) {
                return false;
            }
            Vector2 pos = GetNodeScreenPos(node.CalculatedPosition);
            int radius = (int)(24f * zoom) + 10;
            Rectangle canvas = layout.Canvas;
            pos.X = MathHelper.Clamp(pos.X, canvas.X + radius, canvas.Right - radius);
            pos.Y = MathHelper.Clamp(pos.Y, canvas.Y + radius, canvas.Bottom - radius);
            rect = new Rectangle((int)pos.X - radius, (int)pos.Y - radius, radius * 2, radius * 2);
            return true;
        }

        #endregion

        private float zoom = 1f;
        private bool isDraggingMap;
        private Vector2 panOffset;
        private Vector2 dragStartMousePos;
        private Vector2 dragStartPanOffset;

        private int oldScrollWheelValue;

        private QuestNode selectedNode;
        private QuestNode selectedNodeTransfers;
        private bool showDetailPanel;
        private readonly AnimatedFloat detailAnim = new(0f, 0.16f);
        private float detailScroll;
        private float detailScrollTarget;

        private QuestNode hoveredNode;

        public bool ShowProgressBar { get; set; } = true;
        public bool NightMode { get; set; } = false;

        public string LocalizationCategory => "UI";

        private readonly QuestLogLauncher launcher;
        public Vector2 LauncherPosition;
        private bool isDraggingLauncher;
        private Vector2 dragStartLauncherPos;
        private Vector2 dragStartMousePosForLauncher;

        public static LocalizedText ObjectiveText;
        public static LocalizedText RewardText;
        public static LocalizedText ReceiveAwardText;
        public static LocalizedText QuickReceiveAwardText;
        public static LocalizedText ProgressText;
        public static LocalizedText StyleSwitchText;
        public static LocalizedText NightModeText;
        public static LocalizedText SunModeText;
        public static LocalizedText ResetViewText;
        public static LocalizedText LauncherHoverText;
        public static LocalizedText QuestManagerText;
        public static LocalizedText ObjectiveTemplateDefeatNpc;
        public static LocalizedText ObjectiveTemplateObtainItem;
        public static LocalizedText ObjectiveTemplateCollectItem;
        public static LocalizedText DisabledOverlayText;

        //「远征纪要」样式的外框文案
        public static LocalizedText ChronicleTitle;
        public static LocalizedText ChronicleProgress;
        public static LocalizedText ChronicleHint;
        public static LocalizedText ChronicleStationChart;
        public static LocalizedText ChronicleStationEntrust;
        public static LocalizedText ChronicleChapterTitle;
        public static LocalizedText ChronicleLegendTitle;
        public static LocalizedText ChronicleLegendSealed;
        public static LocalizedText ChronicleLegendUnclaimed;
        public static LocalizedText ChronicleLegendActive;
        public static LocalizedText ChronicleLegendLocked;

        private List<IQuestLogStyle> availableStyles;
        private int currentStyleIndex;

        /// <summary>「远征纪要」在样式表中的位置，新档默认</summary>
        public const int ChronicleStyleIndex = 3;

        //旧存档是否已被顶到新样式一次
        private bool chronicleMigrated;

        public QuestLog() {
            launcher = new QuestLogLauncher();
            LauncherPosition = new Vector2(572, 108);

            //索引 0~2 是旧皮肤（存档里已按此序号存过，不可重排）；3 是新的门面样式
            availableStyles = [
                new HotwindQuestLogStyle(),
                new DraedonQuestLogStyle(),
                new ForestQuestLogStyle(),
                new ChronicleQuestLogStyle()
            ];
            currentStyleIndex = ChronicleStyleIndex;
            CurrentStyle = availableStyles[currentStyleIndex];
        }

        /// <summary>按索引设样式，sync则同步委托管理器</summary>
        public void SetStyleByIndex(int index, bool sync = true) {
            if (availableStyles == null || availableStyles.Count == 0) return;
            currentStyleIndex = Math.Clamp(index, 0, availableStyles.Count - 1);
            CurrentStyle = availableStyles[currentStyleIndex];
            if (sync) {
                QuestManagerUI.Instance?.SetStyleByIndex(currentStyleIndex, false);
            }
        }

        public override void SetStaticDefaults() {
            ObjectiveText = this.GetLocalization(nameof(ObjectiveText), () => "任务目标");
            RewardText = this.GetLocalization(nameof(RewardText), () => "任务奖励");
            ReceiveAwardText = this.GetLocalization(nameof(ReceiveAwardText), () => "领取奖励");
            QuickReceiveAwardText = this.GetLocalization(nameof(QuickReceiveAwardText), () => "一键领取");
            ProgressText = this.GetLocalization(nameof(ProgressText), () => "任务完成比例");
            StyleSwitchText = this.GetLocalization(nameof(StyleSwitchText), () => "切换风格");
            NightModeText = this.GetLocalization(nameof(NightModeText), () => "夜间模式");
            SunModeText = this.GetLocalization(nameof(SunModeText), () => "日间模式");
            ResetViewText = this.GetLocalization(nameof(ResetViewText), () => "重置视图");
            LauncherHoverText = this.GetLocalization(nameof(LauncherHoverText), () => "左键开关面板，右键拖动");
            QuestManagerText = this.GetLocalization(nameof(QuestManagerText), () => "委托任务");
            ObjectiveTemplateDefeatNpc = this.GetLocalization("ObjectiveTemplate.DefeatNpc", () => "Defeat {0}");
            ObjectiveTemplateObtainItem = this.GetLocalization("ObjectiveTemplate.ObtainItem", () => "Obtain {0}");
            ObjectiveTemplateCollectItem = this.GetLocalization("ObjectiveTemplate.CollectItem", () => "Collect {0} {1}");
            DisabledOverlayText = this.GetLocalization(nameof(DisabledOverlayText), () => "任务检测已在当前世界中被禁止\n重新进入世界以重新选择配置");
            ChronicleTitle = this.GetLocalization(nameof(ChronicleTitle), () => "远 征 纪 要");
            ChronicleProgress = this.GetLocalization(nameof(ChronicleProgress), () => "已结 {0} / {1}");
            ChronicleHint = this.GetLocalization(nameof(ChronicleHint), () => "滚轮 缩放   ·   拖动 平移   ·   Esc 合卷");
            ChronicleStationChart = this.GetLocalization(nameof(ChronicleStationChart), () => "任务图谱");
            ChronicleStationEntrust = this.GetLocalization(nameof(ChronicleStationEntrust), () => "委托卷宗");
            ChronicleChapterTitle = this.GetLocalization(nameof(ChronicleChapterTitle), () => "章 目");
            ChronicleLegendTitle = this.GetLocalization(nameof(ChronicleLegendTitle), () => "图 例");
            ChronicleLegendSealed = this.GetLocalization(nameof(ChronicleLegendSealed), () => "已结卷");
            ChronicleLegendUnclaimed = this.GetLocalization(nameof(ChronicleLegendUnclaimed), () => "待领赏");
            ChronicleLegendActive = this.GetLocalization(nameof(ChronicleLegendActive), () => "在行中");
            ChronicleLegendLocked = this.GetLocalization(nameof(ChronicleLegendLocked), () => "未启程");
        }

        public override void SaveUIData(TagCompound tag) {
            tag[Name + ":" + nameof(zoom)] = zoom;
            tag[Name + ":" + nameof(panOffset)] = panOffset;
            tag[Name + ":" + nameof(dragStartMousePos)] = dragStartMousePos;
            tag[Name + ":" + nameof(dragStartPanOffset)] = dragStartPanOffset;
            tag[Name + ":" + nameof(currentStyleIndex)] = currentStyleIndex;
            tag[Name + ":" + nameof(LauncherPosition)] = LauncherPosition;
            tag[Name + ":" + nameof(chronicleMigrated)] = true;
            //记住上次摊在哪一站，下次开书直接翻回去
            tag[Name + ":" + nameof(View)] = (byte)View;
        }

        public override void LoadUIData(TagCompound tag) {
            tag.TryGet(Name + ":" + nameof(zoom), out zoom);
            zoom = MathHelper.Clamp(zoom, 0.4f, 2.0f);
            tag.TryGet(Name + ":" + nameof(panOffset), out panOffset);
            tag.TryGet(Name + ":" + nameof(dragStartMousePos), out dragStartMousePos);
            tag.TryGet(Name + ":" + nameof(dragStartPanOffset), out dragStartPanOffset);
            tag.TryGet(Name + ":" + nameof(currentStyleIndex), out currentStyleIndex);
            currentStyleIndex = (int)MathHelper.Clamp(currentStyleIndex, 0, availableStyles.Count - 1);
            //新样式上线前的存档只存过 0~2，读回来会把玩家钉在旧皮肤上。
            //一次性把它顶到「远征纪要」，此后尊重玩家自己的选择
            tag.TryGet(Name + ":" + nameof(chronicleMigrated), out chronicleMigrated);
            if (!chronicleMigrated) {
                currentStyleIndex = ChronicleStyleIndex;
                chronicleMigrated = true;
            }
            SetStyleByIndex(currentStyleIndex, false);
            tag.TryGet(Name + ":" + nameof(LauncherPosition), out LauncherPosition);
            if (LauncherPosition == Vector2.Zero) {
                LauncherPosition = new Vector2(572, 108);
            }
            if (tag.TryGet(Name + ":" + nameof(View), out byte savedView)
                && savedView == (byte)QuestLogView.Entrust) {
                View = QuestLogView.Entrust;
            }
        }

        protected override void OnOpen() {
            //全屏摊开，背包让位
            Main.playerInventory = false;
            CloseDetail(false);
            isDraggingMap = false;
            if (VaultUtils.isSinglePlayer) {
                WorldFreezeSystem.Activate(FreezeReason);
            }
        }

        /// <summary>切站点，详情随之收起</summary>
        public void SetView(QuestLogView view) {
            if (View == view) {
                return;
            }
            View = view;
            CloseDetail(false);
            isDraggingMap = false;
            hoveredNode = null;
        }

        /// <summary>开书并翻到委托卷宗；已经摊在那儿就什么都不做。教程兜底走这条，不用会来回开关的 Toggle</summary>
        public void OpenEntrustView() {
            SetView(QuestLogView.Entrust);
            if (!IsOpen) {
                Open();
            }
        }

        /// <summary>把第 index 条章目推到画布中心并摊开它的记录条，供教程兜底演示</summary>
        public bool FocusAndOpenChapter(int index) {
            if (index < 0 || index >= chapterRoots.Count) {
                return false;
            }
            FocusNode(chapterRoots[index]);
            OpenDetail(chapterRoots[index]);
            return true;
        }

        /// <summary>开书并翻到委托卷宗；已在该站点则合书</summary>
        public void ToggleEntrustView() {
            if (EntrustViewActive) {
                Close();
                return;
            }
            SetView(QuestLogView.Entrust);
            if (!IsOpen) {
                Open();
            }
            else {
                SoundEngine.PlaySound(SoundID.MenuTick);
            }
        }

        protected override void OnClose() {
            CloseDetail(false);
            isDraggingMap = false;
            hoveredNode = null;
            if (VaultUtils.isSinglePlayer) {
                WorldFreezeSystem.Deactivate(FreezeReason);
            }
        }

        public override void LogicUpdate() {
            //逻辑帧更样式与内部动画，防高帧加速
            CurrentStyle?.UpdateStyle();
            if (chapterRoots.Count == 0 || Main.GameUpdateCount % 30 == 0) {
                RefreshChapterRoots();
            }
            detailAnim.TweenTo(showDetailPanel ? 1f : 0f);
            detailAnim.Update();
            detailScroll = MathHelper.Lerp(detailScroll, detailScrollTarget, 0.2f);
        }

        public override void Update() {
            //滚轮基准每帧都要吃掉，合书期间不读就会在开书首帧收到一个巨量增量
            int scrollDelta = ReadScrollDelta();

            mainPanelAlpha = QuestLogTheme.EaseOutCubic(OpenProgress.Current);
            layout = QuestLogTheme.Layout(QuestLogTheme.EaseOutCubic(detailAnim.Current));
            //命中判定阶段就交付分区，样式的按钮矩形与绘制读同一份
            CurrentStyle.SyncLayout(in layout);
            UIHitBox = layout.Full;
            hoverInMainPage = IsOpen;

            UpdateLauncher();

            if (!IsOpen && OpenProgress.Current <= 0.01f) {
                return;
            }

            if (IsOpen) {
                //整屏接管：指针、滚轮换武器、背包配方栏滚动，三者每帧常驻
                Main.playerInventory = false;
                player.mouseInterface = true;
                UIInputGuard.SuppressWeaponSwitch();
                PlayerInput.LockVanillaMouseScroll("CalamityOverhaul/QuestLog");
            }

            if (!IsOpen || OpenProgress.Current < 0.9f) {
                return;
            }

            //Esc 第一下退详情，CloseOnEscape 此时为 false 所以基类不会抢先合书
            if (showDetailPanel && Main.keyState.IsKeyDown(Keys.Escape) && Main.oldKeyState.IsKeyUp(Keys.Escape)) {
                CloseDetail();
                return;
            }

            //教程卡浮在书页上：它占住的地方不能再落到图谱或页脚键上，
            //否则点「下一步」会连带把整张图拖走。只掐输入，分区照常交付
            //委托卷宗的行矩形正是教程自己要用来定位的东西，不能因为悬停就停更
            bool guideBlocking = QuestBookGuideRenderer.PointerBlock.Contains(Main.MouseScreen.ToPoint());
            if (guideBlocking) {
                player.mouseInterface = true;
            }

            //页眉合卷/重开必须在卡片短路之前判定，卡叠到「?」上时 || 会把 UpdateChrome 整段吞掉
            bool hoveredHeader = UpdateHeaderButtons();
            bool hoveredChrome = hoveredHeader || guideBlocking || (!guideBlocking && UpdateChrome());
            bool hoveredDetail = !guideBlocking && UpdateDetailPanel(scrollDelta);

            if (View == QuestLogView.Chart) {
                UpdateCanvas(scrollDelta, hoveredChrome || hoveredDetail);
            }
            else {
                //委托卷宗铺在同一块内容区里，输入交给它自己处理
                QuestManagerUI.Instance?.UpdateEmbedded(layout.Canvas, hoveredChrome, scrollDelta);
            }
        }

        /// <summary>左栏被指到的章目行，样式据此提亮；-1 为无</summary>
        public int HoveredChapter { get; private set; } = -1;

        /// <summary>左栏站点页签与章目，返回指针是否落在左栏可点区域。左栏是全样式标配</summary>
        private bool UpdateRail() {
            HoveredChapter = -1;
            Point mouse = Main.MouseScreen.ToPoint();
            bool hovered = false;
            for (int i = 0; i < StationCount; i++) {
                Rectangle tab = QuestLogTheme.RailTab(in layout, i);
                if (!tab.Contains(mouse)) {
                    continue;
                }
                hovered = true;
                if (keyLeftPressState == KeyPressState.Pressed) {
                    QuestLogView target = StationAt(i);
                    if (View != target) {
                        SetView(target);
                        SoundEngine.PlaySound(SoundID.MenuTick);
                    }
                }
            }

            //章目与图例是自绘外框样式的左页内容，旧样式的左栏只有站点页签，别留隐形命中区
            if (View != QuestLogView.Chart || !CurrentStyle.DrawsOwnChrome) {
                return hovered;
            }

            int capacity = Math.Min(chapterRoots.Count, QuestLogTheme.RailChapterCapacity(in layout));
            for (int i = 0; i < capacity; i++) {
                Rectangle row = QuestLogTheme.RailChapter(in layout, i);
                if (!row.Contains(mouse)) {
                    continue;
                }
                hovered = true;
                HoveredChapter = i;
                if (keyLeftPressState == KeyPressState.Pressed) {
                    FocusNode(chapterRoots[i]);
                    SoundEngine.PlaySound(SoundID.MenuTick);
                }
            }
            return hovered;
        }

        /// <summary>背包里的启动图标，仅合书状态下响应</summary>
        private void UpdateLauncher() {
            if (!Main.playerInventory || IsOpen) {
                return;
            }

            if (launcher.IsHovered) {
                player.mouseInterface = true;
                if (keyLeftPressState == KeyPressState.Pressed) {
                    Open();
                }
                //右键拖动图标
                if (keyRightPressState == KeyPressState.Pressed && !isDraggingLauncher) {
                    isDraggingLauncher = true;
                    dragStartLauncherPos = LauncherPosition;
                    dragStartMousePosForLauncher = Main.MouseScreen;
                }
            }

            if (isDraggingLauncher) {
                LauncherPosition = dragStartLauncherPos + (Main.MouseScreen - dragStartMousePosForLauncher);
                if (keyRightPressState == KeyPressState.Released) {
                    isDraggingLauncher = false;
                }
            }

            launcher.Update(LauncherPosition);
        }

        private int ReadScrollDelta() {
            int wheel = Mouse.GetState().ScrollWheelValue;
            int delta = wheel - oldScrollWheelValue;
            oldScrollWheelValue = wheel;
            return delta;
        }

        /// <summary>页眉合卷与重看教程，卡片占住内容区时这两枚键仍要能点</summary>
        private bool UpdateHeaderButtons() {
            Point mouse = Main.MouseScreen.ToPoint();
            bool hovered = false;

            if (layout.MainClose.Contains(mouse)) {
                hovered = true;
                if (keyLeftPressState == KeyPressState.Pressed) {
                    Close();
                    return true;
                }
            }

            if (layout.MainHelp.Contains(mouse)) {
                hovered = true;
                if (keyLeftPressState == KeyPressState.Pressed) {
                    QuestBookGuidePlayer guide = QuestBookGuideFlow.LocalPlayer;
                    if (guide != null) {
                        guide.RestartFromHelp();
                        SoundEngine.PlaySound(SoundID.MenuTick);
                    }
                    return true;
                }
            }

            return hovered;
        }

        /// <summary>左栏与页脚总控，返回指针是否落在其中之一上</summary>
        private bool UpdateChrome() {
            bool hovered = UpdateRail();
            Point mouse = Main.MouseScreen.ToPoint();
            //旧样式的按钮矩形按宿主矩形推算，宿主统一为页脚带，与新样式同构
            Rectangle chrome = layout.Footer;

            if (View == QuestLogView.Chart && !showDetailPanel && HasUnclaimedRewards()) {
                Rectangle claimRect = CurrentStyle.GetClaimAllButtonRect(chrome);
                if (claimRect.Contains(mouse)) {
                    hovered = true;
                    if (keyLeftPressState == KeyPressState.Pressed) {
                        ClaimAllRewards();
                        SoundEngine.PlaySound(SoundID.Grab);
                    }
                }
            }

            if (View == QuestLogView.Chart && panOffset.Length() > 100f) {
                Rectangle resetRect = CurrentStyle.GetResetViewButtonRect(chrome);
                if (resetRect.Contains(mouse)) {
                    hovered = true;
                    if (keyLeftPressState == KeyPressState.Pressed) {
                        ResetView();
                        SoundEngine.PlaySound(SoundID.MenuTick);
                    }
                }
            }

            Rectangle styleRect = CurrentStyle.GetStyleSwitchButtonRect(chrome);
            if (styleRect.Contains(mouse)) {
                hovered = true;
                if (keyLeftPressState == KeyPressState.Pressed) {
                    SetStyleByIndex((currentStyleIndex + 1) % availableStyles.Count);
                    SoundEngine.PlaySound(SoundID.MenuTick);
                }
            }

            if (CurrentStyle.SupportsNightMode) {
                Rectangle nightRect = CurrentStyle.GetNightModeButtonRect(chrome);
                if (nightRect.Contains(mouse)) {
                    hovered = true;
                    if (keyLeftPressState == KeyPressState.Pressed) {
                        NightMode = !NightMode;
                        SoundEngine.PlaySound(SoundID.MenuTick);
                    }
                }
            }

            return hovered;
        }

        /// <summary>右侧详情栏交互，返回指针是否落在栏内</summary>
        private bool UpdateDetailPanel(int scrollDelta) {
            if (!showDetailPanel || selectedNode == null || layout.DetailProgress < 0.5f) {
                return false;
            }

            Rectangle detailRect = layout.Detail;
            bool inside = detailRect.Contains(Main.MouseScreen.ToPoint());
            if (!inside) {
                return false;
            }

            player.mouseInterface = true;

            //正文滚动，溢出量由样式测量
            float contentH = CurrentStyle.MeasureDetailHeight(selectedNode, in layout);
            float maxScroll = MathF.Max(0f, contentH - detailRect.Height);
            if (scrollDelta != 0 && maxScroll > 0.5f) {
                detailScrollTarget = MathHelper.Clamp(detailScrollTarget - scrollDelta * 0.35f, 0f, maxScroll);
            }
            detailScrollTarget = MathHelper.Clamp(detailScrollTarget, 0f, maxScroll);

            Rectangle closeButtonRect = CurrentStyle.GetCloseButtonRect(detailRect);
            if (closeButtonRect.Contains(Main.MouseScreen.ToPoint())
                && keyLeftPressState == KeyPressState.Pressed) {
                CloseDetail();
                return true;
            }

            if (selectedNode.IsCompleted && selectedNode.Rewards != null
                && selectedNode.Rewards.Exists(r => !r.Claimed)) {
                Rectangle buttonRect = CurrentStyle.GetRewardButtonRect(detailRect);
                if (buttonRect.Contains(Main.MouseScreen.ToPoint())
                    && keyLeftPressState == KeyPressState.Pressed) {
                    ClaimRewards(selectedNode);
                    SoundEngine.PlaySound(SoundID.Grab);
                }
            }

            return true;
        }

        /// <summary>画布平移、缩放与节点点选</summary>
        private void UpdateCanvas(int scrollDelta, bool consumedElsewhere) {
            Rectangle canvas = layout.Canvas;
            bool inCanvas = canvas.Contains(Main.MouseScreen.ToPoint()) && !consumedElsewhere;

            if (!inCanvas) {
                isDraggingMap = false;
                hoveredNode = null;
                return;
            }

            //以指针为锚缩放
            if (scrollDelta != 0) {
                float oldZoom = zoom;
                float newZoom = MathHelper.Clamp(zoom + (scrollDelta > 0 ? 0.1f : -0.1f), 0.4f, 2.0f);
                if (oldZoom != newZoom) {
                    Vector2 relativeMouse = Main.MouseScreen - layout.CanvasCenter;
                    panOffset = relativeMouse - (relativeMouse - panOffset) * (newZoom / oldZoom);
                    zoom = newZoom;
                }
            }

            hoveredNode = null;
            foreach (var node in Nodes) {
                if (node.IsHiddenNow) {
                    continue;
                }
                Vector2 nodePos = GetNodeScreenPos(node.CalculatedPosition);
                if (!canvas.Contains(nodePos.ToPoint())) {
                    continue;
                }
                if (Vector2.Distance(Main.MouseScreen, nodePos) < 24f * zoom) {
                    hoveredNode = node;
                    break;
                }
            }

            if (keyLeftPressState == KeyPressState.Pressed) {
                if (hoveredNode != null) {
                    OpenDetail(hoveredNode);
                }
                else {
                    isDraggingMap = true;
                    dragStartMousePos = Main.MouseScreen;
                    dragStartPanOffset = panOffset;
                }
            }

            if (keyLeftPressState == KeyPressState.Held && isDraggingMap) {
                panOffset = dragStartPanOffset + (Main.MouseScreen - dragStartMousePos);
            }

            if (keyLeftPressState == KeyPressState.Released) {
                isDraggingMap = false;
            }
        }

        private void OpenDetail(QuestNode node) {
            selectedNode = node;
            showDetailPanel = true;
            detailScroll = detailScrollTarget = 0f;
            SoundEngine.PlaySound(SoundID.MenuTick);
        }

        private void CloseDetail(bool playSound = true) {
            if (showDetailPanel && playSound) {
                SoundEngine.PlaySound(SoundID.MenuClose);
            }
            showDetailPanel = false;
            selectedNode = null;
            detailScrollTarget = 0f;
        }

        private void ClaimRewards(QuestNode node) {
            if (node.Rewards == null) return;

            Player player = Main.LocalPlayer;
            foreach (var reward in node.Rewards) {
                if (!reward.Claimed) {
                    player.GiveItem(player.GetSource_GiftOrReward(), reward.ItemType, reward.Amount);
                    reward.Claimed = true;
                }
            }
        }

        private void ClaimAllRewards() {
            foreach (var node in Nodes) {
                if (node.IsCompleted && node.Rewards != null) {
                    ClaimRewards(node);
                }
            }
        }

        private bool HasUnclaimedRewards() {
            foreach (var node in Nodes) {
                if (node.IsCompleted && node.Rewards != null && node.Rewards.Exists(r => !r.Claimed)) {
                    return true;
                }
            }
            return false;
        }

        private void ResetView() {
            dragStartPanOffset = panOffset = Vector2.Zero;
            zoom = 1f;
        }

        /// <summary>把某个节点平移到画布中心</summary>
        public void FocusNode(QuestNode node) {
            if (node == null) {
                return;
            }
            dragStartPanOffset = panOffset = -node.CalculatedPosition * zoom;
        }

        /// <summary>教程兜底的演示平移：小步推一段图，让玩家看见图是能拖动的</summary>
        public void PanChartBy(Vector2 delta) {
            if (View != QuestLogView.Chart) {
                return;
            }
            dragStartPanOffset = panOffset += delta;
        }

        /// <summary>重扫章目，节点表在世界内不常变，每 30 帧一次足够</summary>
        private void RefreshChapterRoots() {
            chapterRoots.Clear();
            foreach (var node in Nodes) {
                //无父节点的根与登记为枢纽的节点都算章目；隐藏且未解锁的不列
                if (node.IsHiddenNow) {
                    continue;
                }
                if (node.ParentIDs == null || node.ParentIDs.Count == 0 || node.IsChapterHub) {
                    chapterRoots.Add(node);
                }
            }
            //起点(无父根)恒在第 0 条，教程按此讲解；其余按 ChapterOrder，ID 兜底保证全序稳定
            chapterRoots.Sort(static (a, b) => {
                int rootA = a.ParentIDs == null || a.ParentIDs.Count == 0 ? 0 : 1;
                int rootB = b.ParentIDs == null || b.ParentIDs.Count == 0 ? 0 : 1;
                if (rootA != rootB) {
                    return rootA.CompareTo(rootB);
                }
                if (a.ChapterOrder != b.ChapterOrder) {
                    return a.ChapterOrder.CompareTo(b.ChapterOrder);
                }
                return string.CompareOrdinal(a.ID, b.ID);
            });
        }

        public override void Draw(SpriteBatch spriteBatch) {
            if (Main.playerInventory && !IsOpen) {
                launcher.Draw(spriteBatch);
                if (launcher.IsHovered) {
                    Main.hoverItemName = LauncherHoverText.Value;
                }
            }

            if (!IsOpen && OpenProgress.Current <= 0.01f) {
                return;
            }

            CurrentStyle.SyncLayout(in layout);

            //背景铺满整屏
            CurrentStyle.DrawBackground(spriteBatch, this, layout.Full);

            bool styleOwnsChrome = CurrentStyle.DrawsOwnChrome;
            if (styleOwnsChrome) {
                CurrentStyle.DrawChrome(spriteBatch, this, in layout);
            }

            if (View == QuestLogView.Chart) {
                DrawCanvasContent(spriteBatch);
            }
            else {
                QuestManagerUI.Instance?.DrawEmbedded(spriteBatch, layout.Canvas, mainPanelAlpha);
            }

            if (!styleOwnsChrome) {
                DrawCloseGlyph(spriteBatch, layout.MainClose, mainPanelAlpha);
                DrawHelpGlyph(spriteBatch, layout.MainHelp, mainPanelAlpha);
            }

            if (showDetailPanel || layout.DetailProgress > 0.01f) {
                if (selectedNode is not null) {
                    selectedNodeTransfers = selectedNode;
                }
                if (selectedNodeTransfers is not null) {
                    CurrentStyle.DrawDetail(spriteBatch, selectedNodeTransfers, in layout,
                        layout.DetailProgress * mainPanelAlpha, detailScroll);
                    if (!styleOwnsChrome) {
                        DrawCloseGlyph(spriteBatch, CurrentStyle.GetCloseButtonRect(layout.Detail),
                            layout.DetailProgress * mainPanelAlpha);
                    }
                }
            }
            else {
                selectedNodeTransfers = null;
            }

            DrawChromeButtons(spriteBatch);

            //检测禁用时的禁止层
            var qlPlayer = Main.LocalPlayer.GetModPlayer<QLPlayer>();
            if (!qlPlayer.ShouldCheckQuestInCurrentWorld()) {
                DrawDisabledOverlay(spriteBatch);
            }
        }

        /// <summary>画布区：连线与节点，裁在画布内</summary>
        private void DrawCanvasContent(SpriteBatch spriteBatch) {
            Rectangle canvas = layout.Canvas;
            Rectangle prevScissor = spriteBatch.GraphicsDevice.ScissorRectangle;

            spriteBatch.End();
            //点采样叠小数字号会让节点名不规则丢像素读作断墨，画布一律各向异性采样
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.AnisotropicClamp,
                DepthStencilState.None, new RasterizerState { ScissorTestEnable = true }, null, Main.UIScaleMatrix);
            spriteBatch.GraphicsDevice.ScissorRectangle = VaultUtils.GetClippingRectangle(spriteBatch, canvas);

            //视口外的节点直接跳过：裁剪只是不显示，绘制指令照样会发出去。
            //连线不在这里剔，样式按调用序号取抖动种子，跳过会让墨路随平移重洗一遍
            Rectangle cull = canvas;
            cull.Inflate(120, 120);

            foreach (var node in Nodes) {
                if (node.IsHiddenNow) {
                    continue;
                }
                foreach (var parentID in node.ParentIDs) {
                    var parent = QuestNode.GetQuest(parentID);
                    if (parent == null || parent.IsHiddenNow) {
                        continue;
                    }
                    Vector2 start = GetNodeScreenPos(parent.CalculatedPosition);
                    Vector2 end = GetNodeScreenPos(node.CalculatedPosition);
                    CurrentStyle.DrawConnection(spriteBatch, start, end, node.IsUnlocked, mainPanelAlpha);
                }
            }

            foreach (var node in Nodes) {
                if (node.IsHiddenNow) {
                    continue;
                }
                Vector2 nodePos = GetNodeScreenPos(node.CalculatedPosition);
                if (!cull.Contains(nodePos.ToPoint())) {
                    continue;
                }
                bool hovered = hoveredNode == node;
                if (node.PreDraw(spriteBatch, nodePos, zoom, hovered, mainPanelAlpha)) {
                    CurrentStyle.DrawNode(spriteBatch, node, nodePos, zoom, hovered, mainPanelAlpha);
                }
                node.PostDraw(spriteBatch, nodePos, zoom, hovered, mainPanelAlpha);
            }

            spriteBatch.End();
            spriteBatch.GraphicsDevice.ScissorRectangle = prevScissor;
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.AnisotropicClamp,
                DepthStencilState.None, RasterizerState.CullCounterClockwise, null, Main.UIScaleMatrix);
        }

        private void DrawChromeButtons(SpriteBatch spriteBatch) {
            Rectangle chrome = layout.Footer;
            Point mouse = Main.MouseScreen.ToPoint();

            CurrentStyle.DrawProgressBar(spriteBatch, this, chrome);

            if (View == QuestLogView.Chart && !showDetailPanel && HasUnclaimedRewards()) {
                Rectangle claimRect = CurrentStyle.GetClaimAllButtonRect(chrome);
                CurrentStyle.DrawClaimAllButton(spriteBatch, chrome, claimRect.Contains(mouse), mainPanelAlpha);
            }

            if (View == QuestLogView.Chart && panOffset.Length() > 100f) {
                Rectangle resetRect = CurrentStyle.GetResetViewButtonRect(chrome);
                bool hovered = resetRect.Contains(mouse);
                if (hovered) {
                    Main.hoverItemName = ResetViewText.Value;
                }
                CurrentStyle.DrawResetViewButton(spriteBatch, chrome, -panOffset, hovered, mainPanelAlpha);
            }

            Rectangle styleRect = CurrentStyle.GetStyleSwitchButtonRect(chrome);
            bool styleHovered = styleRect.Contains(mouse);
            CurrentStyle.DrawStyleSwitchButton(spriteBatch, chrome, styleHovered, mainPanelAlpha);
            if (styleHovered) {
                Main.hoverItemName = StyleSwitchText.Value;
            }

            if (CurrentStyle.SupportsNightMode) {
                Rectangle nightRect = CurrentStyle.GetNightModeButtonRect(chrome);
                bool nightHovered = nightRect.Contains(mouse);
                CurrentStyle.DrawNightModeButton(spriteBatch, chrome, nightHovered, mainPanelAlpha, NightMode);
                if (nightHovered) {
                    Main.hoverItemName = NightMode ? NightModeText.Value : SunModeText.Value;
                }
            }

            //旧样式不自绘左页，站点书口由容器补一版同族暗色的通用件
            if (!CurrentStyle.DrawsOwnChrome) {
                DrawGenericRailTabs(spriteBatch);
            }
        }

        /// <summary>
        /// 通用站点页签：暗钢底 + 状态受光缘 + 标签，给不自绘外框的旧样式。<br/>
        /// 中性灰族，压得住热风/嘉登/森林三套底色
        /// </summary>
        private void DrawGenericRailTabs(SpriteBatch spriteBatch) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Point mouse = Main.MouseScreen.ToPoint();
            for (int i = 0; i < StationCount; i++) {
                Rectangle tab = QuestLogTheme.RailTab(in layout, i);
                QuestLogView station = StationAt(i);
                bool selected = View == station;
                bool isHovered = tab.Contains(mouse);

                //贴身投影 + 底板
                spriteBatch.Draw(pixel, new Rectangle(tab.X + 2, tab.Y + 2, tab.Width, tab.Height),
                    Color.Black * (mainPanelAlpha * 0.4f));
                Color bg = selected ? new Color(52, 52, 60)
                    : isHovered ? new Color(40, 40, 47) : new Color(28, 28, 33);
                spriteBatch.Draw(pixel, tab, bg * mainPanelAlpha);

                //受光上缘与吃暗下缘，不描四边框
                Color lip = selected ? new Color(200, 200, 210) : new Color(120, 120, 130);
                spriteBatch.Draw(pixel, new Rectangle(tab.X, tab.Y, tab.Width, 1),
                    lip * (mainPanelAlpha * 0.55f));
                spriteBatch.Draw(pixel, new Rectangle(tab.X, tab.Bottom - 1, tab.Width, 1),
                    Color.Black * (mainPanelAlpha * 0.6f));
                //选中：左缘一道亮楔
                if (selected) {
                    spriteBatch.Draw(pixel, new Rectangle(tab.X, tab.Y + 2, 2, tab.Height - 4),
                        new Color(225, 225, 235) * (mainPanelAlpha * 0.8f));
                }

                string label = station == QuestLogView.Chart
                    ? ChronicleStationChart?.Value ?? string.Empty
                    : ChronicleStationEntrust?.Value ?? string.Empty;
                Color textColor = selected ? Color.White : new Color(168, 168, 178);
                Utils.DrawBorderString(spriteBatch, label,
                    new Vector2(tab.X + 12f, tab.Y + 7f), textColor * mainPanelAlpha, 0.78f);
            }
        }

        private float disabledOverlayAnimTime;

        private void DrawDisabledOverlay(SpriteBatch spriteBatch) {
            disabledOverlayAnimTime += 0.016f;

            Texture2D pixel = VaultAsset.placeholder2.Value;
            Rectangle full = layout.Full;

            float pulseAlpha = 0.65f + MathF.Sin(disabledOverlayAnimTime * 2f) * 0.05f;
            Color overlayColor = new Color(150, 50, 50) * (mainPanelAlpha * pulseAlpha);
            spriteBatch.Draw(pixel, full, overlayColor);

            Vector2 center = new Vector2(full.X + full.Width / 2f, full.Y + full.Height / 2f);

            float circleRadius = 60f;
            float circleThickness = 8f;
            Color circleColor = new Color(200, 60, 60) * (mainPanelAlpha * 0.8f);

            //SoftGlow发光
            Texture2D softGlow = CWRAsset.SoftGlow.Value;
            float glowPulse = 0.8f + MathF.Sin(disabledOverlayAnimTime * 3f) * 0.2f;
            Color glowColor = new Color(200, 80, 80, 0) * (mainPanelAlpha * 0.4f * glowPulse);
            spriteBatch.Draw(softGlow, center, null, glowColor, 0f,
                softGlow.Size() / 2f, 2f, SpriteEffects.None, 0f);

            int segments = 36;
            for (int i = 0; i < segments; i++) {
                float angle1 = MathHelper.TwoPi * i / segments;
                float angle2 = MathHelper.TwoPi * (i + 1) / segments;

                Vector2 p1 = center + angle1.ToRotationVector2() * circleRadius;
                Vector2 p2 = center + angle2.ToRotationVector2() * circleRadius;

                float segAngle = MathF.Atan2(p2.Y - p1.Y, p2.X - p1.X);
                float segLength = Vector2.Distance(p1, p2);

                spriteBatch.Draw(pixel, p1, new Rectangle(0, 0, 1, 1), circleColor,
                    segAngle, new Vector2(0, 0.5f), new Vector2(segLength + 1, circleThickness), SpriteEffects.None, 0f);
            }

            float lineAngle = MathHelper.PiOver4;
            float lineLength = circleRadius * 1.4f;
            Vector2 lineStart = center - lineAngle.ToRotationVector2() * lineLength / 2f;

            spriteBatch.Draw(pixel, lineStart, new Rectangle(0, 0, 1, 1), circleColor,
                lineAngle, new Vector2(0, 0.5f), new Vector2(lineLength, circleThickness), SpriteEffects.None, 0f);

            string text = DisabledOverlayText?.Value ?? "任务检测已被禁止";
            string[] lines = text.Split('\n');

            float textY = center.Y + circleRadius + 30f;
            float lineHeight = FontAssets.MouseText.Value.MeasureString("A").Y;

            for (int i = 0; i < lines.Length; i++) {
                Vector2 textSize = FontAssets.MouseText.Value.MeasureString(lines[i]);
                Vector2 textPos = new Vector2(center.X - textSize.X / 2f * 0.9f, textY + i * lineHeight);

                Utils.DrawBorderString(spriteBatch, lines[i], textPos + new Vector2(2, 2),
                    Color.Black * (mainPanelAlpha * 0.6f), 0.9f);

                //文字+脉冲
                Color textColor = Color.Lerp(new Color(255, 180, 180), new Color(255, 100, 100),
                    MathF.Sin(disabledOverlayAnimTime * 2f) * 0.5f + 0.5f);
                Utils.DrawBorderString(spriteBatch, lines[i], textPos, textColor * mainPanelAlpha, 0.9f);
            }
        }

        /// <summary>通用合卷键，仅在样式不自绘外框时使用</summary>
        private static void DrawCloseGlyph(SpriteBatch spriteBatch, Rectangle rect, float alpha) {
            bool hovered = rect.Contains(Main.MouseScreen.ToPoint());
            Texture2D pixel = VaultAsset.placeholder2.Value;

            Color bgC = hovered ? new Color(80, 40, 40) * (alpha * 0.4f)
                : new Color(10, 10, 10) * (alpha * 0.35f);
            spriteBatch.Draw(pixel, rect, bgC);

            Color xColor = hovered ? new Color(255, 100, 100) * alpha
                : new Color(180, 180, 180) * (alpha * 0.6f);
            float cx = rect.X + rect.Width / 2f;
            float cy = rect.Y + rect.Height / 2f;
            float xSize = rect.Width * 0.22f;
            spriteBatch.Draw(pixel, new Vector2(cx, cy), null, xColor,
                MathHelper.PiOver4, new Vector2(0.5f), new Vector2(xSize * 2f, 1.5f), SpriteEffects.None, 0f);
            spriteBatch.Draw(pixel, new Vector2(cx, cy), null, xColor,
                -MathHelper.PiOver4, new Vector2(0.5f), new Vector2(xSize * 2f, 1.5f), SpriteEffects.None, 0f);
        }

        /// <summary>通用重看教程键，仅在样式不自绘外框时使用</summary>
        private static void DrawHelpGlyph(SpriteBatch spriteBatch, Rectangle rect, float alpha) {
            bool hovered = rect.Contains(Main.MouseScreen.ToPoint());
            if (hovered) {
                Main.hoverItemName = QuestBookGuideLead.HelpButtonHover.Value;
            }
            Texture2D pixel = VaultAsset.placeholder2.Value;

            Color bgC = hovered ? new Color(60, 60, 40) * (alpha * 0.4f)
                : new Color(10, 10, 10) * (alpha * 0.35f);
            spriteBatch.Draw(pixel, rect, bgC);

            Color inkColor = hovered ? new Color(255, 226, 176) * alpha
                : new Color(180, 180, 180) * (alpha * 0.6f);
            var font = FontAssets.MouseText.Value;
            Vector2 size = font.MeasureString("?") * 0.92f;
            Utils.DrawBorderString(spriteBatch, "?",
                new Vector2(rect.X + (rect.Width - size.X) * 0.5f, rect.Y + (rect.Height - size.Y) * 0.5f),
                inkColor, 0.92f);
        }

        private Vector2 GetNodeScreenPos(Vector2 nodePos) {
            return layout.CanvasCenter + panOffset + nodePos * zoom;
        }
    }
}

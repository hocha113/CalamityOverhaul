using CalamityOverhaul.Common;
using CalamityOverhaul.Content.EntrustManager;
using CalamityOverhaul.Content.Narrative;
using CalamityOverhaul.Content.Narrative.Guides;
using System.Collections.Generic;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI;

namespace CalamityOverhaul.Content.QuestLogs.Guide
{
    /// <summary>
    /// 任务书教程的排队入口与文案库。状态机在 <see cref="QuestBookGuidePlayer"/>，
    /// 绘制在 <see cref="QuestBookGuideRenderer"/>，这里只管"轮不轮得到讲"和"讲什么词"
    /// </summary>
    internal class QuestBookGuideLead : ModSystem, ILocalizedModType, IGuideLead
    {
        public string LocalizationCategory => "UI";

        /// <summary>队列饿死放弃后的让位时长，让被压住的引导有机会先讲</summary>
        private const int ReserveDeferFrames = 60 * 60;

        private static int reserveDeferTicks;

        /// <summary>卡片上呼吸与巡笔的时间源，单位秒，与「远征纪要」样式同口径</summary>
        public static float ShaderTimer { get; private set; }

        /// <summary>让位期立即作废，书里点「?」重开教程时用</summary>
        public static void ClearReserveDefer() => reserveDeferTicks = 0;

        #region 文案

        //第一章 开卷
        public static LocalizedText WelcomeTitle { get; private set; }
        public static LocalizedText WelcomeLine1 { get; private set; }
        public static LocalizedText WelcomeLine2 { get; private set; }
        public static LocalizedText RailTitle { get; private set; }
        public static LocalizedText RailLine1 { get; private set; }
        public static LocalizedText RailLine2 { get; private set; }
        public static LocalizedText RailLine3 { get; private set; }
        public static LocalizedText ChartViewTitle { get; private set; }
        public static LocalizedText ChartViewLine1 { get; private set; }
        public static LocalizedText ChartViewLine2 { get; private set; }
        public static LocalizedText ChartViewAct { get; private set; }
        public static LocalizedText ChartNodeTitle { get; private set; }
        public static LocalizedText ChartNodeLine1 { get; private set; }
        public static LocalizedText ChartNodeAct { get; private set; }
        public static LocalizedText ChartDetailTitle { get; private set; }
        public static LocalizedText ChartDetailLine1 { get; private set; }
        public static LocalizedText ChartDetailLine2 { get; private set; }
        public static LocalizedText OutroTitle { get; private set; }
        public static LocalizedText OutroLine1 { get; private set; }
        public static LocalizedText OutroLine2 { get; private set; }

        //第二章 委托
        public static LocalizedText KeyPromptTitle { get; private set; }
        public static LocalizedText KeyPromptLine1 { get; private set; }
        public static LocalizedText KeyPromptBound { get; private set; }
        public static LocalizedText KeyPromptUnboundTitle { get; private set; }
        public static LocalizedText KeyPromptUnboundLine { get; private set; }
        public static LocalizedText KeyPromptBindHint { get; private set; }
        public static LocalizedText GotoTitle { get; private set; }
        public static LocalizedText GotoLine1 { get; private set; }
        public static LocalizedText GotoAct { get; private set; }
        public static LocalizedText AnatomyTitle { get; private set; }
        public static LocalizedText AnatomyLine1 { get; private set; }
        public static LocalizedText AnatomyLine2 { get; private set; }
        public static LocalizedText AnatomyAct { get; private set; }
        public static LocalizedText TrackTitle { get; private set; }
        public static LocalizedText TrackLine1 { get; private set; }
        public static LocalizedText TrackAct { get; private set; }
        public static LocalizedText TrackerTitle { get; private set; }
        public static LocalizedText TrackerLine1 { get; private set; }
        public static LocalizedText TrackerLine2 { get; private set; }
        public static LocalizedText TrackerLine3 { get; private set; }
        public static LocalizedText SuspendTitle { get; private set; }
        public static LocalizedText SuspendLine1 { get; private set; }
        public static LocalizedText SuspendLine2 { get; private set; }

        //按钮
        public static LocalizedText BtnConfirm { get; private set; }
        public static LocalizedText BtnNext { get; private set; }
        public static LocalizedText BtnSkipStep { get; private set; }
        public static LocalizedText BtnDismiss { get; private set; }
        public static LocalizedText BtnOpenBook { get; private set; }

        /// <summary>书内「?」键的悬停提示</summary>
        public static LocalizedText HelpButtonHover { get; private set; }

        public override void SetStaticDefaults() {
            GuideLeadQueue.Register(this);

            WelcomeTitle = this.GetLocalization(nameof(WelcomeTitle), () => "这本书");
            WelcomeLine1 = this.GetLocalization(nameof(WelcomeLine1), () => "书里摊着两样东西：任务图谱与委托卷宗。");
            WelcomeLine2 = this.GetLocalization(nameof(WelcomeLine2), () => "图谱记主线走到哪一步，卷宗收各路人马托付给你的活儿。");

            RailTitle = this.GetLocalization(nameof(RailTitle), () => "左栏的两枚书口");
            RailLine1 = this.GetLocalization(nameof(RailLine1), () => "上面一枚翻到 任务图谱。");
            RailLine2 = this.GetLocalization(nameof(RailLine2), () => "下面一枚翻到 委托卷宗。");
            RailLine3 = this.GetLocalization(nameof(RailLine3), () => "点书口就换站点，书不用合上。");

            ChartViewTitle = this.GetLocalization(nameof(ChartViewTitle), () => "摊开图谱");
            ChartViewLine1 = this.GetLocalization(nameof(ChartViewLine1), () => "滚轮缩放，按住左键拖动平移。");
            ChartViewLine2 = this.GetLocalization(nameof(ChartViewLine2), () => "视角跑远了，用页脚右侧的 归位 键拉回来。");
            ChartViewAct = this.GetLocalization(nameof(ChartViewAct), () => "试着滚一下滚轮，或者把图拖开一段。");

            ChartNodeTitle = this.GetLocalization(nameof(ChartNodeTitle), () => "点开一个节点");
            ChartNodeLine1 = this.GetLocalization(nameof(ChartNodeLine1), () => "节点上的记号就是它的状态：影线未启程，墨环在行中，蜡封待领赏，裂开的蜡封已结卷。");
            ChartNodeAct = this.GetLocalization(nameof(ChartNodeAct), () => "左键点圈出来的这一枚。");

            ChartDetailTitle = this.GetLocalization(nameof(ChartDetailTitle), () => "右侧的记录条");
            ChartDetailLine1 = this.GetLocalization(nameof(ChartDetailLine1), () => "上半是这一节要做的事，下半是做完能领的东西。");
            ChartDetailLine2 = this.GetLocalization(nameof(ChartDetailLine2), () => "页脚的 一键领取 会把所有已结卷的赏一次收齐。");

            OutroTitle = this.GetLocalization(nameof(OutroTitle), () => "委托卷宗还空着");
            OutroLine1 = this.GetLocalization(nameof(OutroLine1), () => "剧情推进时会有人把活儿托到这一站。");
            OutroLine2 = this.GetLocalization(nameof(OutroLine2), () => "接到第一份委托时，这份教程会接着往下讲。");

            KeyPromptTitle = this.GetLocalization(nameof(KeyPromptTitle), () => "有人托了活儿给你");
            KeyPromptLine1 = this.GetLocalization(nameof(KeyPromptLine1), () => "第一份委托已经收进卷宗，翻开看看。");
            KeyPromptBound = this.GetLocalization(nameof(KeyPromptBound), () => "按 [{0}] 打开任务书。");
            KeyPromptUnboundTitle = this.GetLocalization(nameof(KeyPromptUnboundTitle), () => "任务书快捷键还没绑");
            KeyPromptUnboundLine = this.GetLocalization(nameof(KeyPromptUnboundLine), () => "默认键是 [{0}]，现在按它也能开。");
            KeyPromptBindHint = this.GetLocalization(nameof(KeyPromptBindHint), () => "要换键去 设置 → 控制 里改。");

            GotoTitle = this.GetLocalization(nameof(GotoTitle), () => "翻到委托卷宗");
            GotoLine1 = this.GetLocalization(nameof(GotoLine1), () => "书会记住你上次停在哪一站，下次开书直接回到这里。");
            GotoAct = this.GetLocalization(nameof(GotoAct), () => "点左栏下面那枚书口。");

            AnatomyTitle = this.GetLocalization(nameof(AnatomyTitle), () => "一条委托");
            AnatomyLine1 = this.GetLocalization(nameof(AnatomyLine1), () => "左边的圆戳是委托人，中间是名目与进度，右边是当前状态。");
            AnatomyLine2 = this.GetLocalization(nameof(AnatomyLine2), () => "展开后能看到正文、进度与落款。");
            AnatomyAct = this.GetLocalization(nameof(AnatomyAct), () => "左键点这一行，把它摊开。");

            TrackTitle = this.GetLocalization(nameof(TrackTitle), () => "关注它");
            TrackLine1 = this.GetLocalization(nameof(TrackLine1), () => "被关注的委托会钉在屏幕左侧的追踪栏里，不开书也看得见进度。");
            TrackAct = this.GetLocalization(nameof(TrackAct), () => "右键单击这一行  →  关注。");

            TrackerTitle = this.GetLocalization(nameof(TrackerTitle), () => "追踪栏");
            TrackerLine1 = this.GetLocalization(nameof(TrackerLine1), () => "左侧这一列常驻显示所有被关注的委托。");
            TrackerLine2 = this.GetLocalization(nameof(TrackerLine2), () => "按住左键上下拖，可以挪它的位置。");
            TrackerLine3 = this.GetLocalization(nameof(TrackerLine3), () => "打开任务书时它会自己收起来，不挡书页。");

            SuspendTitle = this.GetLocalization(nameof(SuspendTitle), () => "挂起与分类");
            SuspendLine1 = this.GetLocalization(nameof(SuspendLine1), () => "中键单击一行  →  挂起，它就不再出现在追踪栏里。");
            SuspendLine2 = this.GetLocalization(nameof(SuspendLine2), () => "顶上四个页签分开放着 进行中 / 全部 / 已完成 / 挂起，挂起的去最后一个里找。");

            BtnConfirm = this.GetLocalization(nameof(BtnConfirm), () => "明白了");
            BtnNext = this.GetLocalization(nameof(BtnNext), () => "下一步");
            BtnSkipStep = this.GetLocalization(nameof(BtnSkipStep), () => "跳过这一步");
            BtnDismiss = this.GetLocalization(nameof(BtnDismiss), () => "收起教程");
            BtnOpenBook = this.GetLocalization(nameof(BtnOpenBook), () => "直接打开");

            HelpButtonHover = this.GetLocalization(nameof(HelpButtonHover), () => "重看任务书教程");
        }

        #endregion

        #region 生命周期

        public override void OnWorldUnload() {
            reserveDeferTicks = 0;
            QuestBookGuideFlow.LocalPlayer?.Suspend();
            QuestBookGuideRenderer.ClearPointerBlock();
        }

        public override void UpdateUI(GameTime gameTime) {
            if (Main.gameMenu) {
                return;
            }
            QuestBookGuidePlayer guide = QuestBookGuideFlow.LocalPlayer;
            if (guide == null) {
                return;
            }

            ShaderTimer += 1f / 60f;
            if (ShaderTimer > 10000f) {
                ShaderTimer -= 10000f;
            }
            if (reserveDeferTicks > 0) {
                reserveDeferTicks--;
            }

            guide.NoteBookOpened();
            guide.Tick(GuideLeadQueue.IsHolder(this));

            if (!QuestBookGuideFlow.IsRunning) {
                QuestBookGuideRenderer.ClearPointerBlock();
            }
        }

        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers) {
            if (!QuestBookGuideFlow.IsRunning || !GuideLeadQueue.IsHolder(this)) {
                return;
            }
            int index = layers.FindIndex(l => l.Name == "Vanilla: Mouse Text");
            if (index == -1) {
                return;
            }
            layers.Insert(index, new LegacyGameInterfaceLayer(
                "CWRMod: Quest Book Guide",
                delegate {
                    QuestBookGuideRenderer.Draw(Main.spriteBatch);
                    return true;
                },
                InterfaceScaleType.UI
            ));
        }

        #endregion

        #region 排队协议

        //晚于比目鱼(10)与义体(15)：任务书是元界面，玩家自己翻开时再讲不迟
        int IGuideLead.GuidePriority => 18;

        bool IGuideLead.GuideReserving => Reserving;

        bool IGuideLead.GuideReady => Ready;

        //队列不再饿死调用这里；接口仍保留，被放弃时只停展示不记拒绝
        void IGuideLead.OnGuideAbandoned() {
            QuestBookGuideFlow.LocalPlayer?.Suspend();
            reserveDeferTicks = ReserveDeferFrames;
        }

        private static bool Reserving {
            get {
                if (reserveDeferTicks > 0) {
                    return false;
                }
                QuestBookGuidePlayer guide = QuestBookGuideFlow.LocalPlayer;
                if (guide == null || guide.Declined) {
                    return false;
                }
                if (guide.ChapterOnePending) {
                    //玩家自己翻开过书才占位，不在开局跟剧情引导抢
                    return guide.BookEverOpened;
                }
                //第二章要等真有委托，否则就是对着空卷宗讲
                return guide.ChapterTwoPending && QuestManagerUI.Instance?.HasAnyEntry == true;
            }
        }

        private static bool Ready {
            get {
                if (!Reserving) {
                    return false;
                }
                if (NarrativeTriggerGate.IsBusy || InnoVault.Cinematics.CutsceneDirector.IsPlaying) {
                    return false;
                }
                QuestBookGuidePlayer guide = QuestBookGuideFlow.LocalPlayer;
                //第一章全程在书里讲，书没摊开就不算就绪
                return !guide.ChapterOnePending || QuestLog.Instance?.IsOpen == true;
            }
        }

        #endregion

        /// <summary>取任务书键的首个绑定；未绑定返回 null</summary>
        public static string BoundKeyName() {
            if (CWRKeySystem.QuestLog_Key == null) {
                return null;
            }
            List<string> keys = CWRKeySystem.QuestLog_Key.GetAssignedKeys();
            return keys.Count > 0 ? keys[0] : null;
        }
    }
}

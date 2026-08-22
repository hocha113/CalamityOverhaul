using CalamityOverhaul.Content.EntrustManager;
using CalamityOverhaul.Content.Narrative.Data;
using CalamityOverhaul.Content.Narrative.Data.Modules;
using CalamityOverhaul.Content.Narrative.Guides;
using CalamityOverhaul.Content.QuestLogs.Core;
using System;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.QuestLogs.Guide
{
    /// <summary>
    /// 任务书教程的状态机。推进、检查点、跳步与兜底都在这里，
    /// 绘制只读它的公开态，渲染层不该有第二份"当前在第几步"
    /// </summary>
    internal class QuestBookGuidePlayer : ModPlayer
    {
        #region 运行态

        public QuestBookStep CurrentStep { get; private set; } = QuestBookStep.None;

        /// <summary>卡片渐显 0~1</summary>
        public float AnimProgress { get; private set; }

        /// <summary>本步已停留的帧数，兜底与「跳过这一步」的现身时机都读它</summary>
        public int StepTimer { get; private set; }

        /// <summary>倒计推进剩余帧，>0 时卡上画确认条</summary>
        public int AutoAdvanceDelay { get; private set; }

        /// <summary>倒计推进总帧，用来算确认条的比例</summary>
        public int AutoAdvanceTotal { get; private set; }

        /// <summary>「跳过这一步」是否该出现了</summary>
        public bool SkipOffered => StepTimer >= QuestBookGuideFlow.SkipButtonDelay
            && AutoAdvanceDelay <= 0;

        /// <summary>ChartNode 步圈定的节点，Targets 每帧据此取实时屏幕矩形</summary>
        public QuestNode ChartTargetNode { get; private set; }

        /// <summary>TrackEntry 进入时样本行是否已被自动关注，文案据此换讲法</summary>
        public bool TrackEntryPreTracked { get; private set; }

        private float viewZoomSnapshot;
        private Vector2 viewPanSnapshot;
        private int trackedSnapshot;
        private int suspendedSnapshot;

        /// <summary>Rail 步内见过图谱以外的站点，见过且回到图谱才算完成了切换</summary>
        private bool railSawOtherView;

        /// <summary>已关注变体里见过关注数下探（玩家取消过一次）</summary>
        private bool trackSawDip;

        /// <summary>ChartView 兜底演示的剩余帧，>0 时替玩家小步拖图</summary>
        private int chartDemoPanTicks;

        #endregion

        private QuestBookGuideData Guide => Player.GetModPlayer<StoryPlayer>().Get<QuestBookGuideData>();

        public override void Initialize() {
            ResetRuntime();
        }

        public override void OnEnterWorld() {
            ResetRuntime();
            MergeLegacyEntrustGuide();
        }

        private void ResetRuntime() {
            CurrentStep = QuestBookStep.None;
            AnimProgress = 0f;
            StepTimer = 0;
            AutoAdvanceDelay = 0;
            AutoAdvanceTotal = 0;
            ChartTargetNode = null;
            TrackEntryPreTracked = false;
            railSawOtherView = false;
            trackSawDip = false;
            chartDemoPanTicks = 0;
        }

        /// <summary>
        /// 老档折算：旧版只讲委托的引导看完过，就当两章都讲过了
        /// 已经会用委托的人不该被重新教一遍
        /// </summary>
        private void MergeLegacyEntrustGuide() {
            QuestBookGuideData guide = Guide;
            if (guide.LegacyEntrustGuideMerged) {
                return;
            }
            guide.LegacyEntrustGuideMerged = true;
            if (Player.GetModPlayer<StoryPlayer>().Get<EntrustGuideData>().GuideSeen) {
                MarkAllChaptersDone();
            }
        }

        #region 排队侧问答

        /// <summary>玩家自己开过书没有。第一章据此才占位，不在开局跟剧情引导抢</summary>
        public bool BookEverOpened => Guide.BookEverOpened;

        public bool Declined => Guide.Declined;

        public bool ChapterOnePending => !Guide.ChapterOneDone
            && Guide.CompletedVersion < QuestBookGuideFlow.TutorialVersion;

        public bool ChapterTwoPending => Guide.ChapterOneDone
            && Guide.CompletedVersion < QuestBookGuideFlow.TutorialVersion;

        /// <summary>开过书就记一笔，不必等教程真的轮到自己</summary>
        public void NoteBookOpened() {
            if (!Guide.BookEverOpened && QuestLog.Instance?.IsOpen == true) {
                Guide.BookEverOpened = true;
            }
        }

        #endregion

        #region 外部指令

        /// <summary>队列把展示权交给本教程后，每帧由引导入口调用</summary>
        public void Tick(bool hasLease) {
            if (!hasLease) {
                Suspend();
                return;
            }

            if (CurrentStep == QuestBookStep.None) {
                //场面还不具备就别起步。第一章每一步都要书摊开着，
                //书没开就起步会当帧被 KeepStepAlive 挂起，下一帧再起
                //空转本身无害，但进步时的场面准备（摊记录条、翻站点）会跟着每帧重放一次
                if (!CanStartNow()) {
                    return;
                }
                StartFromCheckpoint();
                if (CurrentStep == QuestBookStep.None) {
                    return;
                }
            }

            AnimProgress = MathHelper.Lerp(AnimProgress, 1f, QuestBookGuideFlow.AnimSpeed);
            if (Main.gamePaused) {
                return;
            }

            if (!KeepStepAlive()) {
                return;
            }

            //进这一步的当帧就已经满足了（书本来就开着、记忆把书翻回了委托站点），
            //别让卡片对着已经做完的事再催一遍。一帧只过一步，链子自然收得住
            if (StepTimer == 0 && AllowsInstantSkip(CurrentStep) && StepSatisfied()) {
                AdvanceStep();
                return;
            }

            StepTimer++;

            if (AutoAdvanceDelay > 0) {
                TickChartDemoPan();
                if (--AutoAdvanceDelay == 0) {
                    AdvanceStep();
                }
                return;
            }

            if (StepSatisfied()) {
                BeginAutoAdvance(QuestBookGuideFlow.AutoActionConfirmDelay);
                return;
            }

            int timeout = QuestBookGuideFlow.IsHandsOn(CurrentStep)
                ? QuestBookGuideFlow.HandsOnTimeout : QuestBookGuideFlow.ExplainTimeout;
            if (StepTimer > timeout) {
                //兜底：能替玩家做的就做一次，让他看见结果；做不了的直接放行
                if (PerformStepForPlayer()) {
                    BeginAutoAdvance(QuestBookGuideFlow.AutoActionConfirmDelay);
                }
                else {
                    AdvanceStep();
                }
            }
        }

        /// <summary>卡上的「知道了 / 下一步」</summary>
        public void ConfirmStep() {
            if (AutoAdvanceDelay > 0) {
                return;
            }
            AdvanceStep();
        }

        /// <summary>卡上的「跳过这一步」</summary>
        public void SkipStep() {
            if (AutoAdvanceDelay > 0) {
                return;
            }
            AdvanceStep();
        }

        /// <summary>卡角的「收起教程」。检查点留着，书里的「?」可以随时重开</summary>
        public void Dismiss() {
            Guide.Declined = true;
            ResetRuntime();
        }

        /// <summary>书内「?」键：清掉婉拒与进度，当场抢走展示权并从第一章开讲</summary>
        public void RestartFromHelp() {
            QuestBookGuideData guide = Guide;
            guide.Declined = false;
            guide.CompletedVersion = 0;
            guide.ChapterOneStep = 0;
            guide.ChapterTwoStep = 0;
            guide.ChapterOneDone = false;
            guide.BookEverOpened = true;
            //可能正卡在队列的让位期里，不清掉的话玩家点了「?」要干等一分钟
            QuestBookGuideLead.ClearReserveDefer();
            ResetRuntime();
            //点「?」是显式要求，不能再等鬼切/比目鱼把队列让出来。
            //ForceHold 发生在绘制帧，本刻 Pump 已经跑过，必须当帧起步卡片才画得出
            QuestBookGuideLead lead = ModContent.GetInstance<QuestBookGuideLead>();
            if (lead != null) {
                GuideLeadQueue.ForceHold(lead);
            }
            if (CanStartNow()) {
                StartFromCheckpoint();
                AnimProgress = 1f;
            }
        }

        /// <summary>失去展示权时挂起。只停展示，绝不写 Declined：缺前置不等于玩家拒绝</summary>
        public void Suspend() {
            if (CurrentStep == QuestBookStep.None) {
                return;
            }
            ResetRuntime();
        }

        #endregion

        #region 推进

        private bool CanStartNow()
            => !ChapterOnePending || QuestLog.Instance?.IsOpen == true;

        private void StartFromCheckpoint() {
            QuestBookGuideData guide = Guide;
            if (guide.Declined || guide.CompletedVersion >= QuestBookGuideFlow.TutorialVersion) {
                return;
            }
            SetStep(guide.ChapterOneDone ? ResolveChapterTwoStart() : ResolveChapterOneStart());
        }

        private QuestBookStep ResolveChapterOneStart() {
            int next = Guide.ChapterOneStep + 1;
            return (QuestBookStep)Math.Clamp(next, (int)QuestBookGuideFlow.ChapterOneFirst,
                (int)QuestBookGuideFlow.ChapterOneLast);
        }

        private QuestBookStep ResolveChapterTwoStart() {
            //第二章的检查点存的也是扁平步号，缺记录时它还落在第一章区间里
            int saved = Guide.ChapterTwoStep;
            if (saved < (int)QuestBookGuideFlow.ChapterTwoFirst) {
                return QuestBookGuideFlow.ChapterTwoFirst;
            }
            return (QuestBookStep)Math.Clamp(saved + 1, (int)QuestBookGuideFlow.ChapterTwoFirst,
                (int)QuestBookGuideFlow.ChapterTwoLast);
        }

        private void SetStep(QuestBookStep step) {
            //讲不通的步直接越过：没有图谱就别讲图谱，没有条目就别讲条目
            int guard = 0;
            while (QuestBookGuideFlow.IsRunningStep(step) && !IsStepMeaningful(step) && guard++ < 16) {
                if (step == QuestBookGuideFlow.ChapterOneLast) {
                    Guide.ChapterOneDone = true;
                    Guide.ChapterOneStep = (int)QuestBookGuideFlow.ChapterOneLast;
                    step = QuestBookStep.None;
                    break;
                }
                if (step == QuestBookGuideFlow.ChapterTwoLast) {
                    MarkAllChaptersDone();
                    step = QuestBookStep.None;
                    break;
                }
                step++;
            }

            CurrentStep = step;
            AnimProgress = 0f;
            StepTimer = 0;
            AutoAdvanceDelay = 0;
            AutoAdvanceTotal = 0;
            if (QuestBookGuideFlow.IsRunningStep(step)) {
                OnStepEnter(step);
            }
        }

        private void AdvanceStep() {
            QuestBookStep finished = CurrentStep;
            if (!QuestBookGuideFlow.IsRunningStep(finished)) {
                return;
            }
            WriteCheckpoint(finished);

            if (finished == QuestBookGuideFlow.ChapterOneLast) {
                Guide.ChapterOneDone = true;
                ResetRuntime();
                return;
            }
            if (finished == QuestBookGuideFlow.ChapterTwoLast) {
                MarkAllChaptersDone();
                ResetRuntime();
                return;
            }
            //导航步同时也是场面失守后的退回点，回来时按检查点续，
            //别把已经讲过的几步再走一遍
            if (finished == QuestBookStep.Rail) {
                SetStep(ResolveChapterOneStart());
                return;
            }
            if (finished is QuestBookStep.KeyPrompt or QuestBookStep.GotoEntrust) {
                SetStep(ResolveChapterTwoStart());
                return;
            }
            SetStep(finished + 1);
        }

        private void WriteCheckpoint(QuestBookStep finished) {
            QuestBookGuideData guide = Guide;
            if (QuestBookGuideFlow.IsChapterOne(finished)) {
                guide.ChapterOneStep = Math.Max(guide.ChapterOneStep, (int)finished);
            }
            else if (QuestBookGuideFlow.IsChapterTwo(finished)) {
                guide.ChapterTwoStep = Math.Max(guide.ChapterTwoStep, (int)finished);
            }
        }

        private void MarkAllChaptersDone() {
            QuestBookGuideData guide = Guide;
            guide.CompletedVersion = QuestBookGuideFlow.TutorialVersion;
            guide.ChapterOneDone = true;
            guide.ChapterOneStep = (int)QuestBookGuideFlow.ChapterOneLast;
            guide.ChapterTwoStep = (int)QuestBookGuideFlow.ChapterTwoLast;
        }

        private void BeginAutoAdvance(int delay) {
            AutoAdvanceDelay = delay;
            AutoAdvanceTotal = delay;
        }

        /// <summary>ChartView 兜底演示：确认条走动期间替玩家平滑拖一小段图，两端减速</summary>
        private void TickChartDemoPan() {
            if (chartDemoPanTicks <= 0 || CurrentStep != QuestBookStep.ChartView) {
                return;
            }
            chartDemoPanTicks--;
            float ease = MathF.Sin(MathHelper.Pi * chartDemoPanTicks / 36f);
            QuestLog.Instance?.PanChartBy(new Vector2(2.2f, 1.2f) * ease);
        }

        #endregion

        #region 进步时的场面准备

        private void OnStepEnter(QuestBookStep step) {
            QuestLog book = QuestLog.Instance;
            QuestManagerUI ui = QuestManagerUI.Instance;

            switch (step) {
                case QuestBookStep.Rail:
                    //进入时就不在图谱，玩家要真切一次站点这步才算做完
                    railSawOtherView = book != null && book.View != QuestLogView.Chart;
                    break;

                //三个图谱步都可能从检查点或存档直接落进来，而存档会把书钉在委托站，
                //进步先把图谱翻上来，别对着委托列表讲缩放讲节点
                case QuestBookStep.ChartView:
                    book?.SetView(QuestLogView.Chart);
                    viewZoomSnapshot = book?.ChartZoom ?? 1f;
                    viewPanSnapshot = book?.ChartPan ?? Vector2.Zero;
                    chartDemoPanTicks = 0;
                    break;

                //把要讲的节点推到画布正中，别让玩家满图找我说的是哪一个；
                //记下它，之后圈定环跟着它的实时位置走
                case QuestBookStep.ChartNode:
                    book?.SetView(QuestLogView.Chart);
                    ChartTargetNode = null;
                    if (book != null && book.ChapterRoots.Count > 0) {
                        ChartTargetNode = book.ChapterRoots[0];
                        book.FocusNode(ChartTargetNode);
                    }
                    break;

                //这一步讲的就是记录条，玩家要是抢先收起来了就再摊开一张
                case QuestBookStep.ChartDetail:
                    book?.SetView(QuestLogView.Chart);
                    if (book?.DetailOpen == false) {
                        book.FocusAndOpenChapter(0);
                    }
                    break;

                //存档可能停在已完成/挂起分类，那里没有样本行可讲，先拉回进行中
                case QuestBookStep.EntryAnatomy:
                case QuestBookStep.TrackEntry:
                    if (ui != null && ui.FirstVisibleEntry == null && ui.HasAnyEntry) {
                        ui.ResetCategoryForGuide();
                    }
                    if (step == QuestBookStep.TrackEntry) {
                        //新委托登记时会被自动关注，样本行多半已在关注中
                        //这时讲「右键→关注」是教反的，换成教一次取消与恢复
                        TrackEntryPreTracked = ui?.FirstVisibleEntry?.Status == QuestEntryStatus.Tracked;
                        trackSawDip = false;
                    }
                    break;

                //追踪栏在书摊开时会自动收起，得先合上书才看得见
                case QuestBookStep.TrackerWidget:
                    if (book?.IsOpen == true) {
                        book.Close();
                    }
                    break;

                case QuestBookStep.SuspendAndCategories:
                    book?.OpenEntrustView();
                    if (ui != null && ui.FirstVisibleEntry == null && ui.HasAnyEntry) {
                        ui.ResetCategoryForGuide();
                    }
                    break;
            }

            trackedSnapshot = ui?.CountByStatus(QuestEntryStatus.Tracked) ?? 0;
            suspendedSnapshot = ui?.CountByStatus(QuestEntryStatus.Suspended) ?? 0;
        }

        #endregion

        #region 完成条件与兜底

        /// <summary>
        /// 只有"翻到某处"这类导航步允许当帧判过。<br/>
        /// 讲解步不许，那会让卡片一闪而过，玩家什么都没读到
        /// </summary>
        private static bool AllowsInstantSkip(QuestBookStep step)
            => step is QuestBookStep.KeyPrompt or QuestBookStep.GotoEntrust;

        private bool StepSatisfied() {
            QuestLog book = QuestLog.Instance;
            QuestManagerUI ui = QuestManagerUI.Instance;

            switch (CurrentStep) {
                //在步内真切到过图谱才算数；进入时就在图谱的，等按钮确认，别闪卡
                case QuestBookStep.Rail:
                    if (book == null) {
                        return false;
                    }
                    if (book.View != QuestLogView.Chart) {
                        railSawOtherView = true;
                        return false;
                    }
                    return railSawOtherView;

                case QuestBookStep.ChartView:
                    return book != null
                        && (MathF.Abs(book.ChartZoom - viewZoomSnapshot) > 0.01f
                            || Vector2.Distance(book.ChartPan, viewPanSnapshot) > 40f);

                case QuestBookStep.ChartNode:
                    return book?.DetailOpen == true;

                //记录条被玩家自己收起来了，说明看完了
                case QuestBookStep.ChartDetail:
                    return book?.DetailOpen == false;

                case QuestBookStep.KeyPrompt:
                    return book?.IsOpen == true;

                case QuestBookStep.GotoEntrust:
                    return book?.EntrustViewActive == true;

                case QuestBookStep.EntryAnatomy:
                    return ui?.FirstVisibleEntry?.IsExpanded == true;

                case QuestBookStep.TrackEntry: {
                    int tracked = ui?.CountByStatus(QuestEntryStatus.Tracked) ?? 0;
                    if (!TrackEntryPreTracked) {
                        return tracked > trackedSnapshot;
                    }
                    //已关注变体教的是一次取消与恢复的往返，恢复回来才算完，
                    //别让教程结束时委托正好从追踪栏消失
                    if (tracked < trackedSnapshot) {
                        trackSawDip = true;
                    }
                    return trackSawDip && tracked >= trackedSnapshot;
                }

                //玩家重新开了书，追踪栏已经缩回去了，别再对着空处讲
                case QuestBookStep.TrackerWidget:
                    return book?.IsOpen == true;

                case QuestBookStep.SuspendAndCategories:
                    return (ui?.CountByStatus(QuestEntryStatus.Suspended) ?? 0) > suspendedSnapshot;

                default:
                    return false;
            }
        }

        /// <summary>超时兜底时替玩家把这一步做掉；做不了返回 false，由调用方直接放行</summary>
        private bool PerformStepForPlayer() {
            QuestLog book = QuestLog.Instance;
            QuestManagerUI ui = QuestManagerUI.Instance;

            switch (CurrentStep) {
                //等了半天没切站点，替他翻过去；本来就在图谱的直接放行
                case QuestBookStep.Rail:
                    if (book == null || book.View == QuestLogView.Chart) {
                        return false;
                    }
                    book.SetView(QuestLogView.Chart);
                    return true;

                //替玩家拖一小段图，让他看见视图是活的；真正的位移在确认条期间逐帧走
                case QuestBookStep.ChartView:
                    if (book == null || book.View != QuestLogView.Chart) {
                        return false;
                    }
                    chartDemoPanTicks = 36;
                    return true;

                case QuestBookStep.ChartNode:
                    return book?.FocusAndOpenChapter(0) == true;

                case QuestBookStep.KeyPrompt:
                    if (book == null || book.IsOpen) {
                        return false;
                    }
                    book.Open();
                    return true;

                case QuestBookStep.GotoEntrust:
                    if (book == null || book.EntrustViewActive) {
                        return false;
                    }
                    book.OpenEntrustView();
                    return true;

                case QuestBookStep.EntryAnatomy: {
                    EntrustEntryData entry = ui?.FirstVisibleEntry;
                    if (entry == null || entry.IsExpanded) {
                        return false;
                    }
                    entry.IsExpanded = true;
                    return true;
                }

                case QuestBookStep.TrackEntry: {
                    string key = ui?.TryGetFirstTrackableKey();
                    return key != null && ui.SetEntryStatus(key, QuestEntryStatus.Tracked);
                }

                default:
                    return false;
            }
        }

        /// <summary>
        /// 场面还站得住吗。站不住时保留检查点后退场或退步，
        /// 不把玩家的进度扔掉
        /// </summary>
        private bool KeepStepAlive() {
            if (!QuestBookGuideFlow.RequiresBookOpen(CurrentStep)) {
                return true;
            }
            QuestLog book = QuestLog.Instance;
            if (book?.IsOpen != true) {
                //第二章有专门的催开书卡，退回去等玩家自己回来
                if (QuestBookGuideFlow.IsChapterTwo(CurrentStep)) {
                    SetStep(QuestBookStep.KeyPrompt);
                    return false;
                }
                //第一章是玩家自己合上了书，安静退场，下次开书从检查点续讲
                Suspend();
                return false;
            }

            //站点也得对。玩家中途翻去了另一站，退回教切站点的那步等他回来，
            //别对着不在场的东西念，也别每帧抢着把站点扳回去
            switch (CurrentStep) {
                case QuestBookStep.ChartView:
                case QuestBookStep.ChartNode:
                case QuestBookStep.ChartDetail:
                    if (book.View != QuestLogView.Chart) {
                        SetStep(QuestBookStep.Rail);
                        return false;
                    }
                    break;

                case QuestBookStep.EntryAnatomy:
                case QuestBookStep.TrackEntry:
                case QuestBookStep.SuspendAndCategories:
                    if (!book.EntrustViewActive) {
                        SetStep(QuestBookStep.GotoEntrust);
                        return false;
                    }
                    break;
            }
            return true;
        }

        private static bool IsStepMeaningful(QuestBookStep step) {
            QuestLog book = QuestLog.Instance;
            QuestManagerUI ui = QuestManagerUI.Instance;

            switch (step) {
                //只剩一个站点时没有"切站点"可讲
                case QuestBookStep.Rail:
                    return book != null && book.StationCount > 1;

                case QuestBookStep.ChartView:
                case QuestBookStep.ChartNode:
                case QuestBookStep.ChartDetail:
                    return QuestLog.ChartEnabled && book?.HasChartNodes == true;

                case QuestBookStep.EntryAnatomy:
                case QuestBookStep.TrackEntry:
                case QuestBookStep.SuspendAndCategories:
                    return ui?.HasAnyEntry == true;

                //追踪栏只在真有关注中的委托时才有东西可指
                case QuestBookStep.TrackerWidget:
                    return EntrustTrackerWidget.Instance != null && ui?.HasTrackedEntries() == true;

                default:
                    return true;
            }
        }

        #endregion
    }
}

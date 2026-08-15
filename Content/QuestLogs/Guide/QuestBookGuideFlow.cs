using Terraria;

namespace CalamityOverhaul.Content.QuestLogs.Guide
{
    /// <summary>任务书教程的步号。两章共用一条扁平序列，检查点直接存步号</summary>
    internal enum QuestBookStep
    {
        None = 0,

        //第一章 开卷：全程在书里讲，单人开书即时停，不抢玩家的操作
        Welcome,
        Rail,
        ChartView,
        ChartNode,
        ChartDetail,
        ChapterOneOutro,

        //第二章 委托：接到第一份委托后才开讲，从催开书起步
        KeyPrompt,
        GotoEntrust,
        EntryAnatomy,
        TrackEntry,
        TrackerWidget,
        SuspendAndCategories,

        Complete,
    }

    /// <summary>步号分章、节奏常量与本地玩家门面</summary>
    internal static class QuestBookGuideFlow
    {
        /// <summary>教程版本。改动步骤内容时 +1，老档会从检查点补讲新增的部分</summary>
        internal const int TutorialVersion = 1;

        #region 分章

        internal const QuestBookStep ChapterOneFirst = QuestBookStep.Welcome;
        internal const QuestBookStep ChapterOneLast = QuestBookStep.ChapterOneOutro;
        internal const QuestBookStep ChapterTwoFirst = QuestBookStep.KeyPrompt;
        internal const QuestBookStep ChapterTwoLast = QuestBookStep.SuspendAndCategories;

        internal static bool IsChapterOne(QuestBookStep step)
            => step >= ChapterOneFirst && step <= ChapterOneLast;

        internal static bool IsChapterTwo(QuestBookStep step)
            => step >= ChapterTwoFirst && step <= ChapterTwoLast;

        internal static bool IsRunningStep(QuestBookStep step)
            => IsChapterOne(step) || IsChapterTwo(step);

        /// <summary>
        /// 本步是否要求书摊开着。为假的两步（催开书、追踪栏）故意在书外讲，
        /// 关书不该把它们判成"玩家跑了"
        /// </summary>
        internal static bool RequiresBookOpen(QuestBookStep step)
            => step != QuestBookStep.KeyPrompt && step != QuestBookStep.TrackerWidget
                && IsRunningStep(step);

        /// <summary>
        /// 本步要不要玩家真动手。动手步的兜底更短，且超时会替玩家做一次；
        /// 讲解步只等一次点击
        /// </summary>
        internal static bool IsHandsOn(QuestBookStep step)
            => step is QuestBookStep.ChartView or QuestBookStep.ChartNode
                or QuestBookStep.KeyPrompt or QuestBookStep.GotoEntrust
                or QuestBookStep.EntryAnatomy or QuestBookStep.TrackEntry;

        #endregion

        #region 节奏

        /// <summary>「跳过这一步」在卡上现身前的静默期</summary>
        internal const int SkipButtonDelay = 60 * 9;

        /// <summary>讲解步的硬兜底：读完这么久还没点，就当读过了</summary>
        internal const int ExplainTimeout = 60 * 60;

        /// <summary>动手步的硬兜底</summary>
        internal const int HandsOnTimeout = 60 * 30;

        /// <summary>替玩家做完一步后停一下，让他看清刚刚发生了什么</summary>
        internal const int AutoActionConfirmDelay = 42;

        /// <summary>卡片渐显速度</summary>
        internal const float AnimSpeed = 0.12f;

        #endregion

        #region 本地门面

        internal static QuestBookGuidePlayer LocalPlayer {
            get {
                Player player = Main.LocalPlayer;
                if (player == null || !player.active || Main.dedServ) {
                    return null;
                }
                return player.TryGetModPlayer(out QuestBookGuidePlayer guide) ? guide : null;
            }
        }

        internal static QuestBookStep CurrentStep => LocalPlayer?.CurrentStep ?? QuestBookStep.None;

        internal static bool IsRunning => IsRunningStep(CurrentStep);

        #endregion
    }
}

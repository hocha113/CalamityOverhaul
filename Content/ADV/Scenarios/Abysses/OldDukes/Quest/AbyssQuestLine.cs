using CalamityOverhaul.Content.ADV.EntrustManager;
using CalamityOverhaul.Content.ADV.Scenarios.Abysses.OldDukes.Campsites;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Abysses.OldDukes.Quest
{
    /// <summary>
    /// 深渊/老公爵委托线：将寻找营地与收集碎片两个委托注册到 <see cref="QuestManagerUI"/>， 并根据 <see cref="ADVSave"/> 实时同步状态
    /// </summary>
    internal class AbyssQuestLine : ModSystem, ILocalizedModType
    {
        public string LocalizationCategory => "ADV";

        #region 常量 Key

        internal const string CAMPSITE_KEY = "Abyss_FindCampsite";
        internal const string FRAGMENT_KEY = "Abyss_FindFragment";

        #endregion

        #region 本地化文本

        public static LocalizedText QuestCategory { get; private set; }

        //寻找营地
        public static LocalizedText CampsiteTitle { get; private set; }
        public static LocalizedText CampsiteSummary { get; private set; }
        public static LocalizedText CampsiteObjective { get; private set; }
        public static LocalizedText CampsiteLocation { get; private set; }
        public static LocalizedText CampsiteDistance { get; private set; }
        public static LocalizedText CampsiteInteract { get; private set; }
        public static LocalizedText CampsiteComplete { get; private set; }
        public static LocalizedText CampsiteHoldHint { get; private set; }

        //收集碎片
        public static LocalizedText FragmentTitle { get; private set; }
        public static LocalizedText FragmentSummary { get; private set; }
        public static LocalizedText FragmentObjective { get; private set; }
        public static LocalizedText FragmentCollect { get; private set; }
        public static LocalizedText FragmentCurrent { get; private set; }
        public static LocalizedText FragmentReturn { get; private set; }
        public static LocalizedText FragmentComplete { get; private set; }
        public static LocalizedText FragmentHint { get; private set; }

        #endregion

        public override void SetStaticDefaults() {
            QuestCategory = this.GetLocalization(nameof(QuestCategory), () => "老公爵");

            CampsiteTitle = this.GetLocalization(nameof(CampsiteTitle), () => "深渊在呼唤");
            CampsiteSummary = this.GetLocalization(nameof(CampsiteSummary), () => "找到并与老公爵对话");
            CampsiteObjective = this.GetLocalization(nameof(CampsiteObjective), () => "目标");
            CampsiteLocation = this.GetLocalization(nameof(CampsiteLocation), () => "前往老公爵营地");
            CampsiteDistance = this.GetLocalization(nameof(CampsiteDistance), () => "距离");
            CampsiteInteract = this.GetLocalization(nameof(CampsiteInteract), () => "与老公爵对话");
            CampsiteComplete = this.GetLocalization(nameof(CampsiteComplete), () => "任务完成！");
            CampsiteHoldHint = this.GetLocalization(nameof(CampsiteHoldHint), () => "持有海洋碎片可查看方向");

            FragmentTitle = this.GetLocalization(nameof(FragmentTitle), () => "深渊在呼唤");
            FragmentSummary = this.GetLocalization(nameof(FragmentSummary), () => "收集777块海洋残片");
            FragmentObjective = this.GetLocalization(nameof(FragmentObjective), () => "目标");
            FragmentCollect = this.GetLocalization(nameof(FragmentCollect), () => "收集海洋残片");
            FragmentCurrent = this.GetLocalization(nameof(FragmentCurrent), () => "当前拥有");
            FragmentReturn = this.GetLocalization(nameof(FragmentReturn), () => "返回营地提交");
            FragmentComplete = this.GetLocalization(nameof(FragmentComplete), () => "任务完成！");
            FragmentHint = this.GetLocalization(nameof(FragmentHint), () => "钓鱼或者搜刮海洋区域的生物");
        }

        public override void PostUpdateEverything() {
            if (Main.dedServ || Main.gameMenu) return;

            var manager = QuestManagerUI.Instance;
            if (manager == null) return;

            if (!Main.LocalPlayer.TryGetADVSave(out var save)) return;

            SyncCampsiteQuest(manager, save);
            SyncFragmentQuest(manager, save);
        }

        private void SyncCampsiteQuest(QuestManagerUI manager, ADVSave save) {
            //已完成首次对话 → 标记完成
            if (save.Get<OldDukeADVData>().OldDukeFirstCampsiteDialogueCompleted) {
                EnsureCampsiteEntry(manager, completed: true);
                manager.SetEntryStatus(CAMPSITE_KEY, QuestEntryStatus.Completed, 1f);
                return;
            }

            //前提条件：玩家接受合作 + 营地已生成
            if (!save.Get<OldDukeADVData>().OldDukeCooperationAccepted || !OldDukeCampsite.IsGenerated) {
                manager.UnregisterQuest(CAMPSITE_KEY);
                return;
            }

            EnsureCampsiteEntry(manager);
        }

        private void SyncFragmentQuest(QuestManagerUI manager, ADVSave save) {
            //已完成 → 标记完成
            if (save.Get<OldDukeADVData>().OldDukeFindFragmentsQuestCompleted) {
                EnsureFragmentEntry(manager, completed: true);
                manager.SetEntryStatus(FRAGMENT_KEY, QuestEntryStatus.Completed, 1f);
                return;
            }

            //前提条件：碎片任务已触发
            if (!save.Get<OldDukeADVData>().OldDukeFindFragmentsQuestTriggered) {
                manager.UnregisterQuest(FRAGMENT_KEY);
                return;
            }

            EnsureFragmentEntry(manager);
        }

        private static void EnsureCampsiteEntry(QuestManagerUI manager, bool completed = false) {
            if (manager.GetEntry(CAMPSITE_KEY) != null) return;

            var entry = new FindCampsiteQuestEntry(CAMPSITE_KEY, CampsiteTitle, CampsiteSummary, QuestCategory) {
                Priority = 60,
                Status = completed ? QuestEntryStatus.Completed : QuestEntryStatus.Active,
                Progress = completed ? 1f : 0f,
                EntryStyle = new SulfseaEntryStyle(),
                TrackerStyle = new SulfseaTrackerWidgetStyle(),
                ObjectiveFormat = CampsiteObjective,
                LocationFormat = CampsiteLocation,
                DistanceFormat = CampsiteDistance,
                InteractFormat = CampsiteInteract,
                QuestCompleteFormat = CampsiteComplete,
                HoldFragmentHintFormat = CampsiteHoldHint
            };
            manager.RegisterQuest(entry);
        }

        private static void EnsureFragmentEntry(QuestManagerUI manager, bool completed = false) {
            if (manager.GetEntry(FRAGMENT_KEY) != null) return;

            var entry = new FindFragmentQuestEntry(FRAGMENT_KEY, FragmentTitle, FragmentSummary, QuestCategory) {
                Priority = 55,
                Status = completed ? QuestEntryStatus.Completed : QuestEntryStatus.Active,
                Progress = completed ? 1f : 0f,
                EntryStyle = new SulfseaEntryStyle(),
                TrackerStyle = new SulfseaTrackerWidgetStyle(),
                ObjectiveFormat = FragmentObjective,
                CollectFormat = FragmentCollect,
                CurrentFormat = FragmentCurrent,
                ReturnFormat = FragmentReturn,
                QuestCompleteFormat = FragmentComplete,
                HintFormat = FragmentHint
            };
            manager.RegisterQuest(entry);
        }
    }
}

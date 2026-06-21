using CalamityOverhaul.Content.EntrustManager;
using CalamityOverhaul.Content.Narrative.Scenarios.OldDuke.Campsites;
using CalamityOverhaul.Content.Narrative.Scenarios.OldDuke.Quest;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Narrative.Scenarios.OldDuke.Quest
{
    internal sealed class AbyssNarrativeQuestLine : ModSystem, ILocalizedModType
    {
        internal const string CampsiteKey = "Abyss_FindCampsite";
        internal const string FragmentKey = "Abyss_FindFragment";

        public string LocalizationCategory => "ADV";

        public static LocalizedText QuestCategory { get; private set; }
        public static LocalizedText CampsiteTitle { get; private set; }
        public static LocalizedText CampsiteSummary { get; private set; }
        public static LocalizedText CampsiteObjective { get; private set; }
        public static LocalizedText CampsiteLocation { get; private set; }
        public static LocalizedText CampsiteDistance { get; private set; }
        public static LocalizedText CampsiteInteract { get; private set; }
        public static LocalizedText CampsiteComplete { get; private set; }
        public static LocalizedText CampsiteHoldHint { get; private set; }
        public static LocalizedText FragmentTitle { get; private set; }
        public static LocalizedText FragmentSummary { get; private set; }
        public static LocalizedText FragmentObjective { get; private set; }
        public static LocalizedText FragmentCollect { get; private set; }
        public static LocalizedText FragmentCurrent { get; private set; }
        public static LocalizedText FragmentReturn { get; private set; }
        public static LocalizedText FragmentComplete { get; private set; }
        public static LocalizedText FragmentHint { get; private set; }

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
            if (Main.dedServ || Main.gameMenu) {
                return;
            }

            QuestManagerUI manager = QuestManagerUI.Instance;
            if (manager == null) {
                return;
            }

            SyncCampsiteQuest(manager);
            SyncFragmentQuest(manager);
        }

        private static void SyncCampsiteQuest(QuestManagerUI manager) {
            if (OldDukeStorySync.Read(d => d.OldDukeFirstCampsiteDialogueCompleted, d => d.OldDukeFirstCampsiteDialogueCompleted)) {
                EnsureCampsiteEntry(manager, completed: true);
                manager.SetEntryStatus(CampsiteKey, QuestEntryStatus.Completed, 1f);
                return;
            }

            if (!OldDukeStorySync.Read(d => d.OldDukeCooperationAccepted, d => d.OldDukeCooperationAccepted)
                || !OldDukeCampsite.IsGenerated) {
                manager.UnregisterQuest(CampsiteKey);
                return;
            }

            EnsureCampsiteEntry(manager);
        }

        private static void SyncFragmentQuest(QuestManagerUI manager) {
            if (OldDukeStorySync.Read(d => d.OldDukeFindFragmentsQuestCompleted, d => d.OldDukeFindFragmentsQuestCompleted)) {
                EnsureFragmentEntry(manager, completed: true);
                manager.SetEntryStatus(FragmentKey, QuestEntryStatus.Completed, 1f);
                return;
            }

            if (!OldDukeStorySync.Read(d => d.OldDukeFindFragmentsQuestTriggered, d => d.OldDukeFindFragmentsQuestTriggered)) {
                manager.UnregisterQuest(FragmentKey);
                return;
            }

            EnsureFragmentEntry(manager);
        }

        private static void EnsureCampsiteEntry(QuestManagerUI manager, bool completed = false) {
            if (manager.GetEntry(CampsiteKey) is EntrustEntryData existing) {
                if (existing is FindCampsiteQuestEntry) {
                    return;
                }
                manager.UnregisterQuest(CampsiteKey);
            }

            FindCampsiteQuestEntry entry = new(CampsiteKey, CampsiteTitle, CampsiteSummary, QuestCategory) {
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
            if (manager.GetEntry(FragmentKey) is EntrustEntryData existing) {
                if (existing is FindFragmentQuestEntry) {
                    return;
                }
                manager.UnregisterQuest(FragmentKey);
            }

            FindFragmentQuestEntry entry = new(FragmentKey, FragmentTitle, FragmentSummary, QuestCategory) {
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

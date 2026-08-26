using CalamityOverhaul.Content.EntrustManager;
using CalamityOverhaul.Content.Scenarios.Draedon.Quest.DeploySignaltowers;
using CalamityOverhaul.Content.Scenarios.Draedon.Quest.DeploySignaltowers.SignalTower;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Draedon.Quest
{
    internal sealed class DraedonQuestLine : ModSystem, ILocalizedModType
    {
        private const string DeployKey = "Draedon_DeploySignaltower";

        public string LocalizationCategory => "ADV.Draedon";

        public static LocalizedText QuestCategory { get; private set; }
        public static LocalizedText DeployTitle { get; private set; }
        public static LocalizedText DeploySummary { get; private set; }
        public static LocalizedText TrackerNearestTarget { get; private set; }
        public static LocalizedText TrackerNodeName { get; private set; }
        public static LocalizedText TrackerInRange { get; private set; }
        public static LocalizedText TrackerDistance { get; private set; }
        public static LocalizedText TrackerDeployProgress { get; private set; }
        public static LocalizedText TrackerQuestComplete { get; private set; }

        public override void SetStaticDefaults() {
            QuestCategory = this.GetLocalization(nameof(QuestCategory), () => "嘉登");
            DeployTitle = this.GetLocalization(nameof(DeployTitle), () => "量子纠缠网络部署");
            DeploySummary = this.GetLocalization(nameof(DeploySummary), () => "在世界各处的目标点位部署10座信号塔");
            TrackerNearestTarget = this.GetLocalization(nameof(TrackerNearestTarget), () => "最近的目标点");
            TrackerNodeName = this.GetLocalization(nameof(TrackerNodeName), () => "[NUM]号纠缠节点");
            TrackerInRange = this.GetLocalization(nameof(TrackerInRange), () => "范围内");
            TrackerDistance = this.GetLocalization(nameof(TrackerDistance), () => "距离");
            TrackerDeployProgress = this.GetLocalization(nameof(TrackerDeployProgress), () => "部署进度");
            TrackerQuestComplete = this.GetLocalization(nameof(TrackerQuestComplete), () => "任务完成!");
        }

        public override void PostUpdateEverything() {
            if (Main.dedServ || Main.gameMenu) {
                return;
            }

            QuestManagerUI manager = QuestManagerUI.Instance;
            if (manager == null) {
                return;
            }

            SyncDeployQuest(manager);
        }

        private static void SyncDeployQuest(QuestManagerUI manager) {
            if (DraedonStorySync.ReadDraedon(d => d.DeploySignaltowerQuestCompleted, d => d.DeploySignaltowerQuestCompleted)) {
                EnsureDeployEntry(manager, completed: true);
                manager.SetEntryStatus(DeployKey, QuestEntryStatus.Completed, 1f);
                return;
            }

            bool worldHasQuest = DSTPlayer.HasDeploySignaltowerQuestByWorld;
            bool playerAccepted = DraedonStorySync.ReadDraedon(
                d => d.DeploySignaltowerQuestAccepted && !d.DeploySignaltowerQuestDeclined,
                d => d.DeploySignaltowerQuestAccepted && !d.DeploySignaltowerQuestDeclined);

            if (!worldHasQuest && !playerAccepted) {
                manager.UnregisterQuest(DeployKey);
                return;
            }

            if (SignalTowerTargetManager.TargetPoints.Count <= 0) {
                return;
            }

            EnsureDeployEntry(manager);
        }

        private static void EnsureDeployEntry(QuestManagerUI manager, bool completed = false) {
            if (manager.GetEntry(DeployKey) is EntrustEntryData existing) {
                if (existing is DeploySignaltowerQuestEntry) {
                    return;
                }

                manager.UnregisterQuest(DeployKey);
            }

            DeploySignaltowerQuestEntry entry = new(DeployKey, DeployTitle, DeploySummary, QuestCategory) {
                Priority = 50,
                Status = completed ? QuestEntryStatus.Completed : QuestEntryStatus.Active,
                Progress = completed ? 1f : 0f,
                Provider = EntrustProviders.Draedon,
                TrackerStyle = new DraedonTrackerWidgetStyle(),
                NearestTargetFormat = TrackerNearestTarget,
                NodeNameFormat = TrackerNodeName,
                InRangeFormat = TrackerInRange,
                DistanceFormat = TrackerDistance,
                DeployProgressFormat = TrackerDeployProgress,
                QuestCompleteFormat = TrackerQuestComplete
            };
            manager.RegisterQuest(entry);
        }
    }
}

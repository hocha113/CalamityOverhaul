using CalamityOverhaul.Content.ADV.EntrustManager;
using CalamityOverhaul.Content.ADV.Scenarios.Draedons.Quest.DeploySignaltowers;
using CalamityOverhaul.Content.ADV.Scenarios.Draedons.Quest.DeploySignaltowers.SignalTower;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Draedons.Quest
{
    /// <summary>嘉登信号塔委托，注册 <see cref="QuestManagerUI"/> 并按 <see cref="ADVSave"/> 同步</summary>
    internal class DraedonQuestLine : ModSystem, ILocalizedModType
    {
        public string LocalizationCategory => "ADV";

        #region 常量 Key

        private const string DEPLOY_KEY = "Draedon_DeploySignaltower";

        #endregion

        #region 本地化文本

        public static LocalizedText QuestCategory { get; private set; }

        public static LocalizedText DeployTitle { get; private set; }
        public static LocalizedText DeploySummary { get; private set; }

        public static LocalizedText TrackerNearestTarget { get; private set; }
        public static LocalizedText TrackerNodeName { get; private set; }
        public static LocalizedText TrackerInRange { get; private set; }
        public static LocalizedText TrackerDistance { get; private set; }
        public static LocalizedText TrackerDeployProgress { get; private set; }
        public static LocalizedText TrackerQuestComplete { get; private set; }

        #endregion

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
            if (Main.dedServ || Main.gameMenu) return;

            var manager = QuestManagerUI.Instance;
            if (manager == null) return;

            if (!Main.LocalPlayer.TryGetADVSave(out var save)) return;

            SyncDeployQuest(manager, save);
        }

        private void SyncDeployQuest(QuestManagerUI manager, ADVSave save) {
            //已完成 → 标记完成
            if (save.Get<DraedonADVData>().DeploySignaltowerQuestCompleted) {
                EnsureDeployEntry(manager, completed: true);
                manager.SetEntryStatus(DEPLOY_KEY, QuestEntryStatus.Completed, 1f);
                return;
            }

            //判断是否应该显示委托：
            //1. 世界中已有目标点（可能是其他玩家接受的）→ 直接显示
            //2. 玩家自己接受了且未拒绝 → 显示
            bool worldHasQuest = DSTPlayer.HasDeploySignaltowerQuestByWorld;
            bool playerAccepted = save.Get<DraedonADVData>().DeploySignaltowerQuestAccepted && !save.Get<DraedonADVData>().DeploySignaltowerQuestDeclined;

            if (!worldHasQuest && !playerAccepted) {
                manager.UnregisterQuest(DEPLOY_KEY);
                return;
            }

            //目标点尚未生成 → 不注册
            if (SignalTowerTargetManager.TargetPoints.Count <= 0) {
                return;
            }

            EnsureDeployEntry(manager);
        }

        private static void EnsureDeployEntry(QuestManagerUI manager, bool completed = false) {
            if (manager.GetEntry(DEPLOY_KEY) != null) return;

            var entry = new DeploySignaltowerQuestEntry(DEPLOY_KEY, DeployTitle, DeploySummary, QuestCategory) {
                Priority = 50,
                Status = completed ? QuestEntryStatus.Completed : QuestEntryStatus.Active,
                Progress = completed ? 1f : 0f,
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

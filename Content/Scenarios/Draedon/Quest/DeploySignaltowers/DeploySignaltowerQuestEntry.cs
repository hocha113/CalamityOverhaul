using CalamityOverhaul.Content.EntrustManager;
using CalamityOverhaul.Content.Scenarios.Draedon.Quest.DeploySignaltowers.SignalTower;
using System.Collections.Generic;
using Terraria;
using Terraria.Localization;

namespace CalamityOverhaul.Content.Scenarios.Draedon.Quest.DeploySignaltowers
{
    /// <summary>信号塔部署委托，追踪最近节点与总体进度</summary>
    internal class DeploySignaltowerQuestEntry : EntrustEntryData
    {
        /// <summary>"最近的目标点" 格式文本</summary>
        public LocalizedText NearestTargetFormat { get; init; }

        /// <summary>"[NUM]号纠缠节点" 节点名称格式</summary>
        public LocalizedText NodeNameFormat { get; init; }

        /// <summary>"范围内" 文本</summary>
        public LocalizedText InRangeFormat { get; init; }

        /// <summary>"距离" 文本</summary>
        public LocalizedText DistanceFormat { get; init; }

        /// <summary>"部署进度" 文本</summary>
        public LocalizedText DeployProgressFormat { get; init; }

        /// <summary>"任务完成!" 文本</summary>
        public LocalizedText QuestCompleteFormat { get; init; }

        private SignalTowerTargetPoint nearestTarget;
        private bool playerInRange;
        private float distanceToTarget;

        public DeploySignaltowerQuestEntry(string key, LocalizedText title, LocalizedText summary, LocalizedText category)
            : base(key, title, summary, category) { }

        //追踪窗口使用极简HUD样式时，需要在内容区顶部预留5px，
        //让标题下划线与首行描述之间留出可见的呼吸空间
        public override float GetTrackerContentTopPadding() => 5f;

        public override void OnUpdate() {
            if (Status == QuestEntryStatus.Completed || Status == QuestEntryStatus.Failed) return;

            int deployed = DeploySignaltowerNarrativeCheck.DeployedTowerCount;
            int total = DeploySignaltowerNarrativeCheck.TargetTowerCount;
            Progress = MathHelper.Clamp(deployed / (float)total, 0f, 1f);
            ProgressLabel = null; //进度文本由 GetTrackerDetails 提供

            nearestTarget = SignalTowerTargetManager.GetNearestTarget(Main.LocalPlayer);
            if (nearestTarget != null) {
                playerInRange = nearestTarget.IsPlayerInRange(Main.LocalPlayer);
                distanceToTarget = Vector2.Distance(Main.LocalPlayer.Center, nearestTarget.WorldPosition) / 16f;
            }
        }

        public override List<string> GetTrackerDetails() {
            int deployed = DeploySignaltowerNarrativeCheck.DeployedTowerCount;
            int total = DeploySignaltowerNarrativeCheck.TargetTowerCount;

            if (deployed >= total) {
                return [
                    QuestCompleteFormat?.Value ?? "Mission Complete!",
                    $"{deployed}/{total}"
                ];
            }

            List<string> lines = [];

            if (nearestTarget != null) {
                string nodeName = (NodeNameFormat?.Value ?? "[NUM]").Replace("[NUM]", (nearestTarget.Index + 1).ToString());
                lines.Add($"{NearestTargetFormat?.Value ?? ""}: {nodeName}");

                if (playerInRange) {
                    lines.Add(InRangeFormat?.Value ?? "In Range");
                }
                else {
                    lines.Add($"{DistanceFormat?.Value ?? "Distance"}: {(int)distanceToTarget}m");
                }
            }

            lines.Add($"{DeployProgressFormat?.Value ?? "Progress"}: {deployed}/{total}");

            return lines;
        }
    }
}

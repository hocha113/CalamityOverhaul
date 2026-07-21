using CalamityOverhaul.Content.EntrustManager;
using CalamityOverhaul.Content.Scenarios.Draedon.Quest.DeploySignaltowers.SignalTower;
using System.Collections.Generic;
using Terraria;
using Terraria.Localization;

namespace CalamityOverhaul.Content.Scenarios.Draedon.Quest.DeploySignaltowers
{
    internal class DeploySignaltowerQuestEntry : EntrustEntryData
    {
        public LocalizedText NearestTargetFormat { get; init; }
        public LocalizedText NodeNameFormat { get; init; }//[NUM]
        public LocalizedText InRangeFormat { get; init; }
        public LocalizedText DistanceFormat { get; init; }
        public LocalizedText DeployProgressFormat { get; init; }
        public LocalizedText QuestCompleteFormat { get; init; }

        private SignalTowerTargetPoint nearestTarget;
        private bool playerInRange;
        private float distanceToTarget;

        public DeploySignaltowerQuestEntry(string key, LocalizedText title, LocalizedText summary, LocalizedText category)
            : base(key, title, summary, category) { }

        //极简HUD内容区顶留5px
        public override float GetTrackerContentTopPadding() => 5f;

        public override void OnUpdate() {
            if (Status == QuestEntryStatus.Completed || Status == QuestEntryStatus.Failed) return;

            int deployed = DeploySignaltowerNarrativeCheck.DeployedTowerCount;
            int total = DeploySignaltowerNarrativeCheck.TargetTowerCount;
            Progress = MathHelper.Clamp(deployed / (float)total, 0f, 1f);
            ProgressLabel = null;//进度由GetTrackerDetails

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

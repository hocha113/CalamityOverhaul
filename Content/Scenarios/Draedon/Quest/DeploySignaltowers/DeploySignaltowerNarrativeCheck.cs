using CalamityOverhaul.Content.Narrative;
using CalamityOverhaul.Content.Scenarios.Draedon.Quest.DeploySignaltowers.SignalTower;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Draedon.Quest.DeploySignaltowers
{
    internal sealed class DeploySignaltowerNarrativeCheck : ModSystem
    {
        private int scenarioCheckTimer;
        private int questCompleteCheckTimer;

        public static int DeployedTowerCount { get; private set; }

        public const int TargetTowerCount = 10;

        public override void PostUpdateEverything() {
            if (Main.dedServ || Main.gameMenu) {
                return;
            }

            UpdateTowerCount();
            CheckFirstTowerScenario();
            CheckQuestComplete();
        }

        private static void UpdateTowerCount() {
            if (!SignalTowerTargetManager.IsGenerated) {
                DeployedTowerCount = 0;
                return;
            }

            int count = 0;
            foreach (SignalTowerTargetPoint point in SignalTowerTargetManager.TargetPoints) {
                if (point.IsCompleted) {
                    count++;
                }
            }

            DeployedTowerCount = count;
        }

        private void CheckFirstTowerScenario() {
            if (!DraedonStorySync.ReadDraedon(d => d.DeploySignaltowerQuestAccepted, d => d.DeploySignaltowerQuestAccepted)
                || DraedonStorySync.ReadDraedon(d => d.DeploySignaltowerFirstTowerBuilt, d => d.DeploySignaltowerFirstTowerBuilt)) {
                return;
            }

            if (DeployedTowerCount <= 0) {
                scenarioCheckTimer = 0;
                return;
            }

            if (++scenarioCheckTimer < 120 || NarrativeTriggerGate.IsBusy) {
                return;
            }

            DraedonStorySync.WriteDraedon(
                d => d.DeploySignaltowerFirstTowerBuilt = true,
                d => d.DeploySignaltowerFirstTowerBuilt = true);
            NarrativeRouter.Begin<FirstTowerBuiltScenario>();
            scenarioCheckTimer = 0;
        }

        private void CheckQuestComplete() {
            if (!DraedonStorySync.ReadDraedon(d => d.DeploySignaltowerQuestAccepted, d => d.DeploySignaltowerQuestAccepted)
                || DraedonStorySync.ReadDraedon(d => d.DeploySignaltowerQuestCompleted, d => d.DeploySignaltowerQuestCompleted)) {
                return;
            }

            if (DeployedTowerCount < TargetTowerCount) {
                questCompleteCheckTimer = 0;
                return;
            }

            if (++questCompleteCheckTimer < 120 || NarrativeTriggerGate.IsBusy) {
                return;
            }

            //旗在起演成功后才落,否则起演失败这一次就把整段完成剧情(含奖励与后续登门)永久跳过(反馈七·#149)
            questCompleteCheckTimer = 0;
            if (!NarrativeRouter.Begin<QuestCompleteScenario>()) {
                return;
            }
            DraedonStorySync.WriteDraedon(
                d => d.DeploySignaltowerQuestCompleted = true,
                d => d.DeploySignaltowerQuestCompleted = true);
            DSTPlayer.HasDeploySignaltowerQuestByWorld = false;
        }

        public override void ClearWorld() {
            DeployedTowerCount = 0;
            scenarioCheckTimer = 0;
            questCompleteCheckTimer = 0;
        }
    }
}

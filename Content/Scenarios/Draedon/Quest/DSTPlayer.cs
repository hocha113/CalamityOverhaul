using CalamityOverhaul.Content.Scenarios.Draedon.Quest.DeploySignaltowers;
using CalamityOverhaul.Content.Scenarios.Draedon.Quest.DeploySignaltowers.SignalTower;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Draedon.Quest
{
    internal class DSTPlayer : ModPlayer
    {
        public static bool HasDeploySignaltowerQuestByWorld;

        public override void OnEnterWorld() {
            SignalTowerTargetPoint nearestTarget = SignalTowerTargetManager.GetNearestTarget(Player);
            if (nearestTarget != null) {
                HasDeploySignaltowerQuestByWorld = true;
                return;
            }

            HasDeploySignaltowerQuestByWorld = false;

            if (InWorldBossPhase.Downed29.Invoke()
                && DraedonStorySync.ReadDraedon(d => d.ExoMechEndingDialogue, d => d.ExoMechEndingDialogue)
                && !DraedonStorySync.ReadDraedon(d => d.DeploySignaltowerQuestCompleted, d => d.DeploySignaltowerQuestCompleted)) {
                DeploySignaltowerScenario.SetTurnOn();
            }
        }
    }
}

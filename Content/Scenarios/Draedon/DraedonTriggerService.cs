using CalamityOverhaul.Content.Narrative;
using CalamityOverhaul.Content.Scenarios.Draedon.Defeats;
using CalamityOverhaul.Content.Scenarios.Draedon.ExoMechdusaSums;
using Terraria;

namespace CalamityOverhaul.Content.Scenarios.Draedon
{
    internal static class DraedonTriggerService
    {
        public static void NotifyExoMechDefeat(int battleDuration = int.MaxValue, float healthPercent = 1f) {
            if (CWRRef.GetBossRushActive() || NarrativeTriggerGate.IsBusy) {
                return;
            }

            DraedonStorySync.WriteDraedon(
                d => d.ExoMechDefeatCount++,
                d => d.ExoMechDefeatCount++);

            int defeatCount = DraedonStorySync.Story.ExoMechDefeatCount;

            if (!DraedonStorySync.ReadDraedon(d => d.ExoMechEndingDialogue, d => d.ExoMechEndingDialogue)) {
                NarrativeRouter.Begin<ExoMechEndingDialogue>();
                return;
            }

            if (defeatCount == 2 && !DraedonStorySync.ReadDraedon(d => d.ExoMechSecondDefeat, d => d.ExoMechSecondDefeat)) {
                NarrativeRouter.Begin<ExoMechSecondDefeat>();
                return;
            }

            if (defeatCount == 3 && !DraedonStorySync.ReadDraedon(d => d.ExoMechThirdDefeat, d => d.ExoMechThirdDefeat)) {
                NarrativeRouter.Begin<ExoMechThirdDefeat>();
                return;
            }

            if (defeatCount > 3 && battleDuration < 60 * 60 * 2) {
                NarrativeRouter.Begin<ExoMechQuickDefeat>();
                return;
            }

            if (defeatCount > 3 && healthPercent < 0.2f) {
                NarrativeRouter.Begin<ExoMechHardDefeat>();
            }
        }

        public static bool BeginExoMechdusaSummon() {
            if (NarrativeTriggerGate.IsBusy) {
                return false;
            }

            return NarrativeRouter.Begin<ExoMechdusaSum>();
        }

        public static void ArmDeploySignaltowerQuest() => Quest.DeploySignaltowers.DeploySignaltowerScenario.SetTurnOn();
    }
}

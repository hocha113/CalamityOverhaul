using InnoVault.Narrative.Progress;
using InnoVault.Narrative.Services;

namespace CalamityOverhaul.Content.Narrative
{
    internal sealed class NarrativeSyncService : INarrativeSyncService
    {
        public void SyncProgress(string scenarioKey, ScenarioProgress progress) {
            // Scenario progress is player-local for now. Gameplay-critical state keeps using
            // existing CWR packets owned by the relevant content system.
        }
    }
}

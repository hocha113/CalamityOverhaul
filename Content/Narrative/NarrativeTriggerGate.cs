using InnoVault.Narrative.Runtime;

namespace CalamityOverhaul.Content.Narrative
{
    internal static class NarrativeTriggerGate
    {
        public static bool IsBusy => NarrativeRunner.IsBusy;
    }
}

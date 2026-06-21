using InnoVault.Narrative.Core;
using InnoVault.Narrative.Runtime;
using System;

namespace CalamityOverhaul.Content.Narrative.Runtime
{
    internal static class NarrativeRouter
    {
        public static bool Begin<T>() where T : NarrativeScenario {
            T scenario = NarrativeScenario.Find<T>();
            return scenario != null && NarrativeRunner.Begin(scenario);
        }

        public static bool Begin(string key) => NarrativeRunner.Begin(key);

        public static bool IsActive<T>() where T : NarrativeScenario
            => NarrativeRunner.IsScenarioActiveOrPending(NarrativeScenario.GetKey<T>());
    }
}

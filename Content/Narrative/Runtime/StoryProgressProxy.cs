using CalamityOverhaul.Content.Narrative.Data;
using InnoVault.Narrative.Progress;
using Terraria;

namespace CalamityOverhaul.Content.Narrative.Runtime
{
    internal sealed class StoryProgressProxy : INarrativeProgressStore
    {
        private static StoryProgress Store => Main.LocalPlayer.GetModPlayer<StoryPlayer>().Get<StoryProgress>();

        public ScenarioProgress GetProgress(string scenarioKey) => Store.GetProgress(scenarioKey);
        public void SetProgress(string scenarioKey, ScenarioProgress progress) => Store.SetProgress(scenarioKey, progress);
        public bool TryGetChoice(string scenarioKey, out string choiceId) => Store.TryGetChoice(scenarioKey, out choiceId);
        public void SetChoice(string scenarioKey, string choiceId) => Store.SetChoice(scenarioKey, choiceId);
        public bool GetFlag(NarrativeProgressKey key) => Store.GetFlag(key);
        public void SetFlag(NarrativeProgressKey key, bool value) => Store.SetFlag(key, value);
        public int GetCounter(NarrativeProgressKey key) => Store.GetCounter(key);
        public void SetCounter(NarrativeProgressKey key, int value) => Store.SetCounter(key, value);
        public string GetString(NarrativeProgressKey key) => Store.GetString(key);
        public void SetString(NarrativeProgressKey key, string value) => Store.SetString(key, value);
    }
}

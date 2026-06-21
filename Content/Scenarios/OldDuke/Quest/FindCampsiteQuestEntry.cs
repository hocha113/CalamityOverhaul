using CalamityOverhaul.Content.EntrustManager;
using CalamityOverhaul.Content.Scenarios.OldDuke.Campsites;
using System.Collections.Generic;
using Terraria;
using Terraria.Localization;

namespace CalamityOverhaul.Content.Scenarios.OldDuke.Quest
{
    internal sealed class FindCampsiteQuestEntry : EntrustEntryData
    {
        public LocalizedText ObjectiveFormat { get; init; }
        public LocalizedText LocationFormat { get; init; }
        public LocalizedText DistanceFormat { get; init; }
        public LocalizedText InteractFormat { get; init; }
        public LocalizedText QuestCompleteFormat { get; init; }
        public LocalizedText HoldFragmentHintFormat { get; init; }

        private float distanceToCampsite;
        private bool canInteract;

        public FindCampsiteQuestEntry(string key, LocalizedText title, LocalizedText summary, LocalizedText category)
            : base(key, title, summary, category) { }

        public override float GetTrackerContentTopPadding() => 5f;

        public override void OnUpdate() {
            if (Status == QuestEntryStatus.Completed || Status == QuestEntryStatus.Failed) {
                return;
            }

            if (OldDukeStorySync.Read(d => d.OldDukeFirstCampsiteDialogueCompleted, d => d.OldDukeFirstCampsiteDialogueCompleted)) {
                Progress = 1f;
                return;
            }

            if (OldDukeCampsite.IsGenerated) {
                distanceToCampsite = Vector2.Distance(Main.LocalPlayer.Center, OldDukeCampsite.CampsitePosition) / 16f;
                canInteract = OldDukeCampsite.CanInteract();
                Progress = MathHelper.Clamp(1f - distanceToCampsite / 3000f, 0f, 0.99f);
            }
        }

        public override List<string> GetTrackerDetails() {
            if (OldDukeStorySync.Read(d => d.OldDukeFirstCampsiteDialogueCompleted, d => d.OldDukeFirstCampsiteDialogueCompleted)) {
                return [QuestCompleteFormat?.Value ?? "Quest Complete!"];
            }

            List<string> lines = [];
            lines.Add($"{ObjectiveFormat?.Value ?? ""}: {LocationFormat?.Value ?? ""}");

            if (OldDukeCampsite.IsGenerated) {
                if (canInteract) {
                    lines.Add($"> {InteractFormat?.Value ?? ""} <");
                }
                else {
                    lines.Add($"{DistanceFormat?.Value ?? ""}: {(int)distanceToCampsite}m");
                }
            }

            lines.Add(HoldFragmentHintFormat?.Value ?? "");
            return lines;
        }
    }
}

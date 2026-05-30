using CalamityOverhaul.Content.ADV.EntrustManager;
using System.Collections.Generic;
using Terraria.Localization;

namespace CalamityOverhaul.Content.LegendWeapon.TrialQuests
{
    internal class LegendTrialQuestEntry : EntrustEntryData
    {
        public LegendTrialDefinition Trial { get; init; }
        public LocalizedText WaitingHint { get; init; }
        public LocalizedText FightingFormat { get; init; }
        public LocalizedText BriefFormat { get; init; }

        private LegendTrialTargetSnapshot snapshot = LegendTrialTargetSnapshot.Inactive;

        public LegendTrialQuestEntry(string key, LocalizedText title, LocalizedText summary, LocalizedText category)
            : base(key, title, summary, category) { }

        public override float GetTrackerContentTopPadding() => 5f;

        public override void OnUpdate() {
            if (Status == QuestEntryStatus.Completed || Status == QuestEntryStatus.Failed
                || Status == QuestEntryStatus.Suspended) {
                return;
            }

            if (Trial?.IsCompleted == true) {
                snapshot = LegendTrialTargetSnapshot.Completed;
                Progress = 1f;
                return;
            }

            snapshot = Trial?.Target?.GetSnapshot() ?? LegendTrialTargetSnapshot.Inactive;
            Progress = snapshot.Progress;
        }

        public override List<string> GetTrackerDetails() {
            var lines = new List<string>(2);

            string brief = BuildBrief();
            if (!string.IsNullOrEmpty(brief)) {
                lines.Add(brief);
            }

            if (!string.IsNullOrEmpty(snapshot.StatusLine)) {
                lines.Add(snapshot.StatusLine);
            }
            else if (!snapshot.IsActive) {
                lines.Add(WaitingHint?.Value ?? "...");
            }
            else {
                lines.Add(string.Format(FightingFormat?.Value ?? "{0}: {1:0%}",
                    snapshot.ActiveName, snapshot.DisplayRatio));
            }

            return lines;
        }

        private string BuildBrief() {
            string list = string.Join(" / ", Trial?.Target?.GetDisplayNames() ?? []);
            if (string.IsNullOrEmpty(list)) {
                return string.Empty;
            }

            string fmt = BriefFormat?.Value;
            return string.IsNullOrEmpty(fmt) ? list : string.Format(fmt, list);
        }
    }
}

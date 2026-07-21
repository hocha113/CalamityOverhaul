using CalamityOverhaul.Content.EntrustManager;
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
        /// <summary>内容未加载，关卡暂不可开</summary>
        public bool Blocked { get; set; }
        /// <summary>受阻提示</summary>
        public LocalizedText BlockedHint { get; set; }

        private LegendTrialTargetSnapshot snapshot = LegendTrialTargetSnapshot.Inactive;

        public LegendTrialQuestEntry(string key, LocalizedText title, LocalizedText summary, LocalizedText category)
            : base(key, title, summary, category) { }

        public override float GetTrackerContentTopPadding() => 5f;

        public override void OnUpdate() {
            if (Status == QuestEntryStatus.Completed || Status == QuestEntryStatus.Failed
                || Status == QuestEntryStatus.Suspended) {
                return;
            }

            //受阻，跳过战斗进度
            if (Blocked) {
                snapshot = LegendTrialTargetSnapshot.Inactive;
                Progress = 0f;
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
            if (Blocked) {
                var blockedLines = new List<string>(1);
                string hint = BlockedHint?.Value;
                if (!string.IsNullOrEmpty(hint)) {
                    blockedLines.Add(hint);
                }
                return blockedLines;
            }

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

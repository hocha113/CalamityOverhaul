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
        /// <summary>下一关因相关内容(如灾厄模组)未加载而暂时无法开始时为 true</summary>
        public bool Blocked { get; set; }
        /// <summary>受阻时展示的提示文本</summary>
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

            //受阻关卡：目标内容未加载，不做战斗进度推算，仅保持等待态
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

using System;
using System.Collections.Generic;
using Terraria.Localization;

namespace CalamityOverhaul.Content.LegendWeapon.TrialQuests
{
    internal sealed class EventLegendTrialTarget : ILegendTrialTarget
    {
        private readonly LocalizedText displayName;
        private readonly LocalizedText activeFormat;
        private readonly Func<bool> activeCheck;
        private readonly Func<bool> completedCheck;
        private readonly Func<bool> availableCheck;

        public EventLegendTrialTarget(LocalizedText displayName, LocalizedText activeFormat, Func<bool> activeCheck, Func<bool> completedCheck, Func<bool> availableCheck = null) {
            this.displayName = displayName;
            this.activeFormat = activeFormat;
            this.activeCheck = activeCheck;
            this.completedCheck = completedCheck;
            this.availableCheck = availableCheck;
        }

        public bool IsAvailable => availableCheck?.Invoke() ?? true;
        public bool IsCompleted => completedCheck?.Invoke() == true;

        public IEnumerable<string> GetDisplayNames() {
            string name = GetDisplayName();
            if (string.IsNullOrEmpty(name)) {
                return [];
            }
            return [name];
        }

        public LegendTrialTargetSnapshot GetSnapshot() {
            if (IsCompleted) {
                return LegendTrialTargetSnapshot.Completed;
            }
            if (activeCheck?.Invoke() == true) {
                string name = GetDisplayName();
                string format = activeFormat?.Value;
                string status = string.IsNullOrEmpty(format) ? name : string.Format(format, name);
                return new LegendTrialTargetSnapshot(true, 0f, 1f, name, status);
            }
            return LegendTrialTargetSnapshot.Inactive;
        }

        private string GetDisplayName() => displayName?.Value ?? string.Empty;
    }
}

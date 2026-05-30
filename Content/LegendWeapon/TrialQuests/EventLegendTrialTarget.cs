using System;
using System.Collections.Generic;

namespace CalamityOverhaul.Content.LegendWeapon.TrialQuests
{
    internal sealed class EventLegendTrialTarget : ILegendTrialTarget
    {
        private readonly string displayName;
        private readonly Func<bool> activeCheck;
        private readonly Func<bool> completedCheck;
        private readonly Func<bool> availableCheck;

        public EventLegendTrialTarget(string displayName, Func<bool> activeCheck, Func<bool> completedCheck, Func<bool> availableCheck = null) {
            this.displayName = displayName ?? string.Empty;
            this.activeCheck = activeCheck;
            this.completedCheck = completedCheck;
            this.availableCheck = availableCheck;
        }

        public bool IsAvailable => availableCheck?.Invoke() ?? true;
        public bool IsCompleted => completedCheck?.Invoke() == true;

        public IEnumerable<string> GetDisplayNames() {
            if (string.IsNullOrEmpty(displayName)) {
                return [];
            }
            return [displayName];
        }

        public LegendTrialTargetSnapshot GetSnapshot() {
            if (IsCompleted) {
                return LegendTrialTargetSnapshot.Completed;
            }
            if (activeCheck?.Invoke() == true) {
                return new LegendTrialTargetSnapshot(true, 0f, 1f, displayName, $"{displayName}: 进行中");
            }
            return LegendTrialTargetSnapshot.Inactive;
        }
    }
}

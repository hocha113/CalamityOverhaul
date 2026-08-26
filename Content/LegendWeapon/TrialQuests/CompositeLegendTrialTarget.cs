using System;
using System.Collections.Generic;
using System.Linq;

namespace CalamityOverhaul.Content.LegendWeapon.TrialQuests
{
    internal sealed class CompositeLegendTrialTarget : ILegendTrialTarget
    {
        private readonly LegendTrialCompositeMode mode;
        private readonly ILegendTrialTarget[] targets;

        public CompositeLegendTrialTarget(LegendTrialCompositeMode mode, params ILegendTrialTarget[] targets) {
            this.mode = mode;
            this.targets = targets?.Where(static t => t != null).ToArray() ?? [];
        }

        public bool IsAvailable {
            get {
                ILegendTrialTarget[] availableTargets = AvailableTargets();
                return mode == LegendTrialCompositeMode.Any
                    ? availableTargets.Length > 0
                    : availableTargets.Length == targets.Length && targets.Length > 0;
            }
        }

        public bool IsCompleted {
            get {
                ILegendTrialTarget[] availableTargets = AvailableTargets();
                if (availableTargets.Length == 0) {
                    return false;
                }
                return mode == LegendTrialCompositeMode.Any
                    ? availableTargets.Any(static t => t.IsCompleted)
                    : availableTargets.All(static t => t.IsCompleted);
            }
        }

        /// <summary>与 <see cref="IsCompleted"/> 同构：Any 任一子目标亲手达成，All 须全部亲手达成</summary>
        public bool IsPersonallyCleared(Func<int, bool> hasKilled) {
            ILegendTrialTarget[] availableTargets = AvailableTargets();
            if (availableTargets.Length == 0) {
                return false;
            }
            return mode == LegendTrialCompositeMode.Any
                ? availableTargets.Any(t => t.IsPersonallyCleared(hasKilled))
                : availableTargets.All(t => t.IsPersonallyCleared(hasKilled));
        }

        public IEnumerable<string> GetDisplayNames() {
            return AvailableTargets().SelectMany(static t => t.GetDisplayNames());
        }

        public LegendTrialTargetSnapshot GetSnapshot() {
            if (IsCompleted) {
                return LegendTrialTargetSnapshot.Completed;
            }

            ILegendTrialTarget[] availableTargets = AvailableTargets();
            if (availableTargets.Length == 0) {
                return LegendTrialTargetSnapshot.Inactive;
            }

            LegendTrialTargetSnapshot[] snapshots = [.. availableTargets.Select(static t => t.GetSnapshot())];
            LegendTrialTargetSnapshot active = snapshots.FirstOrDefault(static s => s.IsActive);
            if (active.IsActive) {
                return active;
            }

            if (mode == LegendTrialCompositeMode.All && snapshots.Length > 0) {
                float progress = snapshots.Average(static s => s.Progress);
                return new LegendTrialTargetSnapshot(false, progress, 1f - progress, string.Empty);
            }

            return LegendTrialTargetSnapshot.Inactive;
        }

        private ILegendTrialTarget[] AvailableTargets() {
            return [.. targets.Where(static t => t.IsAvailable)];
        }
    }
}

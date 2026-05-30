using CalamityOverhaul.Content.LegendWeapon.TrialQuests;
using System.Collections.Generic;

namespace CalamityOverhaul.Content.LegendWeapon.MurasamaLegend
{
    internal class MuraData : LegendData
    {
        internal override IReadOnlyList<LegendTrialDefinition> TrialDefinitions => LegendTrialRouteCatalog.MurasamaProgression;

        public override int TargetLevel => GetVersionedTrialTargetLevel();
    }
}

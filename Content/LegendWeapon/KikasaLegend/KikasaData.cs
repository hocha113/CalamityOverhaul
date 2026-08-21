using CalamityOverhaul.Content.LegendWeapon.TrialQuests;
using System.Collections.Generic;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend
{
    /// <summary>鬼伞传奇成长数据:等级由沉宴试炼路线推进,只承载伤害缩放</summary>
    internal class KikasaData : LegendData
    {
        internal override IReadOnlyList<LegendTrialDefinition> TrialDefinitions
            => LegendTrialRouteCatalog.KikasaProgression;

        public override int TargetLevel => GetVersionedTrialTargetLevel();
    }
}

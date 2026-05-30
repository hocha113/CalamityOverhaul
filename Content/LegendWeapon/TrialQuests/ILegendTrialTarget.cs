using System.Collections.Generic;

namespace CalamityOverhaul.Content.LegendWeapon.TrialQuests
{
    internal interface ILegendTrialTarget
    {
        bool IsAvailable { get; }
        bool IsCompleted { get; }
        IEnumerable<string> GetDisplayNames();
        LegendTrialTargetSnapshot GetSnapshot();
    }
}

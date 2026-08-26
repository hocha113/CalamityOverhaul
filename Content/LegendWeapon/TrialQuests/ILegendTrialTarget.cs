using System;
using System.Collections.Generic;

namespace CalamityOverhaul.Content.LegendWeapon.TrialQuests
{
    internal interface ILegendTrialTarget
    {
        bool IsAvailable { get; }
        bool IsCompleted { get; }
        /// <summary>按玩家击杀登记口径是否达成，hasKilled 查 <see cref="LegendTrialKillLedgerPlayer"/></summary>
        bool IsPersonallyCleared(Func<int, bool> hasKilled);
        IEnumerable<string> GetDisplayNames();
        LegendTrialTargetSnapshot GetSnapshot();
    }
}

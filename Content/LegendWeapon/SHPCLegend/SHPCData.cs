using CalamityOverhaul.Content.LegendWeapon.TrialQuests;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend
{
    internal class SHPCData : LegendData
    {
        //六个槽位 0=BARREL 1=OPTIC 2=POWER 3=STOCK 4=GRIP 5=FRAME
        public const int SlotCount = 6;

        internal override IReadOnlyList<LegendTrialDefinition> TrialDefinitions => LegendTrialRouteCatalog.SHPCProgression;

        public override int TargetLevel => GetVersionedTrialTargetLevel();

        /// <summary>从Item取SHPCData，否则null</summary>
        public static SHPCData TryGet(Item item) {
            if (item == null || item.IsAir) {
                return null;
            }
            return item.CWR()?.LegendData as SHPCData;
        }
    }
}

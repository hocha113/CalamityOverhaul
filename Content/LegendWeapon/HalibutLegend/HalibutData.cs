using CalamityOverhaul.Content.LegendWeapon.TrialQuests;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend
{
    internal class HalibutData : LegendData
    {
        internal override IReadOnlyList<LegendTrialDefinition> TrialDefinitions => LegendTrialRouteCatalog.HalibutProgression;

        public override int TargetLevel => GetVersionedTrialTargetLevel();

        /// <summary>武器成长等级</summary>
        public static int GetLevel() => GetLevel(Main.LocalPlayer.GetItem());

        /// <summary>武器成长等级</summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public static int GetLevel(Item item) {
            if (item.type != HalibutOverride.ID || !item.Alives()) {
                return 0;
            }
            CWRItem cwrItem = item.CWR();
            if (cwrItem == null) {
                return 0;
            }
            if (cwrItem.LegendData == null) {
                return 0;
            }

            return cwrItem.LegendData.Level;
        }

        /// <summary>本地玩家领域层数</summary>
        public static int GetDomainLayer() => GetDomainLayer(Main.LocalPlayer);

        /// <summary>指定玩家领域层数</summary>
        public static int GetDomainLayer(Player player) {
            if (player.TryGetOverride<HalibutPlayer>(out var halibutPlayer)) {
                return (int)MathHelper.Max(halibutPlayer.SeaDomainLayers, 1);
            }
            return 1;
        }
    }
}

using CalamityOverhaul.Content.LegendWeapon.SHPCLegend;
using CalamityOverhaul.Content.Narrative.Data.Modules;
using InnoVault.Narrative.Core;
using InnoVault.Narrative.Runtime;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Shepel
{
    internal interface ISHPCRoutableNarrative
    {
        int DialoguePriority { get; }
        int RequiredPhase { get; }
        bool CanRoute(Player player);
    }

    internal abstract class ShepelReactiveNarrative : NarrativeScenario, ISHPCRoutableNarrative, ILocalizedModType
    {
        public string LocalizationCategory => "ADV.Shepel";

        public virtual int DialoguePriority => 50;
        public virtual int RequiredPhase => 0;

        protected abstract ShepelReactiveEvent HandledEvent { get; }
        protected virtual int TargetBossNpcType => -1;

        public override StyleId DefaultStyle => "SHPC";

        public bool CanRoute(Player player) {
            if (!player.HasItem(SHPCOverride.ID)) {
                return false;
            }

            ShepelStoryData data = ShepelStorySync.Story;
            if (data.StoryPhase < RequiredPhase) {
                return false;
            }

            if ((data.ReactiveEventFlags & (int)HandledEvent) == 0) {
                return false;
            }

            if (TargetBossNpcType != -1 && data.LastDefeatedBossNpcType != TargetBossNpcType) {
                return false;
            }

            return CheckExtraConditions(player, data);
        }

        protected virtual bool CheckExtraConditions(Player player, ShepelStoryData data) => true;

        protected override void OnStarted() => ShepelNarrativePortrait.Show();

        protected override void OnCompleted() => ShepelNarrativePortrait.Hide();

        protected void ConsumeEvent() {
            ShepelReactiveEvents.ClearFlag(ShepelStorySync.Story, HandledEvent);
        }

        protected override NarrativePolicy ConfigurePolicy() => null;
    }

    internal abstract class ShepelSituationalNarrative : NarrativeScenario, ISHPCRoutableNarrative, ILocalizedModType
    {
        public string LocalizationCategory => "ADV.Shepel";

        public virtual int DialoguePriority => 45;
        public virtual int RequiredPhase => 0;

        public override StyleId DefaultStyle => "SHPC";

        public bool CanRoute(Player player) {
            if (!player.HasItem(SHPCOverride.ID)) {
                return false;
            }

            ShepelStoryData data = ShepelStorySync.Story;
            if (data.StoryPhase < RequiredPhase) {
                return false;
            }

            return CheckConditions(player, data);
        }

        protected abstract bool CheckConditions(Player player, ShepelStoryData data);

        protected override void OnStarted() => ShepelNarrativePortrait.Show();

        protected override void OnCompleted() => ShepelNarrativePortrait.Hide();

        protected override NarrativePolicy ConfigurePolicy() => null;
    }

    internal static class SHPCNarrativeRouter
    {
        private static readonly List<ISHPCRoutableNarrative> routes = [];

        public static void RegisterAll() {
            routes.Clear();
            foreach (NarrativeScenario scenario in NarrativeScenario.All) {
                if (scenario is ISHPCRoutableNarrative route) {
                    routes.Add(route);
                }
            }
        }

        public static bool TryStart(Player player) {
            if (NarrativeRunner.IsBusy) {
                return false;
            }

            if (routes.Count == 0) {
                RegisterAll();
            }

            foreach (ISHPCRoutableNarrative route in routes.OrderByDescending(r => r.DialoguePriority)) {
                if (!route.CanRoute(player)) {
                    continue;
                }

                if (route is NarrativeScenario scenario && NarrativeRunner.Begin(scenario)) {
                    return true;
                }

                return false;
            }

            return false;
        }
    }
}

using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Narrative.Scenarios.SupCal.End.EternalBlazingNow
{
    internal sealed class EternalBlazingNowChoice1 : NarrativeScenario
    {
        public override StyleId DefaultStyle => "Brimstone";

        protected override void Build(NarrativeComposer n) {
            n.Say("Helen", "Solemn", EternalBlazingNow.Choice1Line1.Value)
             .Say("Helen", "Silence", EternalBlazingNow.Choice1Line2.Value, onExit: WitchFarewell.RequestSpawn);
        }

        protected override void OnStarted() => EbnEffect.IsActive = true;
    }
}

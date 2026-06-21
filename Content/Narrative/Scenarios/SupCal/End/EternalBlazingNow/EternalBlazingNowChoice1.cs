using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Narrative.Scenarios.SupCal.End.EternalBlazingNow
{
    internal sealed class EternalBlazingNowChoice1 : NarrativeScenario, ILocalizedModType
    {
        public string LocalizationCategory => "ADV.EternalBlazingNow";

        public static LocalizedText Choice1Line1 { get; private set; }
        public static LocalizedText Choice1Line2 { get; private set; }

        public override StyleId DefaultStyle => "Brimstone";

        public override void SetStaticDefaults() {
            Choice1Line1 = this.GetLocalization(nameof(Choice1Line1), () => "......这就是你的选择吗？");
            Choice1Line2 = this.GetLocalization(nameof(Choice1Line2), () => "我明白了......");
        }

        protected override void Build(NarrativeComposer n) {
            n.Say("Helen", "Solemn", Choice1Line1.Value)
             .Say("Helen", "Silence", Choice1Line2.Value, onExit: WitchFarewell.RequestSpawn);
        }

        protected override void OnStarted() => EbnEffect.IsActive = true;
    }
}

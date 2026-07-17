using CalamityOverhaul.Content.Narrative;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Himayo
{
    internal class ItCametoThis : NarrativeScenario, ILocalizedModType
    {
        public string LocalizationCategory => "ADV";
        public static LocalizedText Line1 { get; private set; }
        public static LocalizedText Line2 { get; private set; }
        public override StyleId DefaultStyle => NarrativeIds.Onikiri;

        public override void SetStaticDefaults() {
            Line1 = this.GetLocalization(nameof(Line1), () => "真不想走到这一步啊");
            Line2 = this.GetLocalization(nameof(Line2), () => "从现在开始，这个世界我接管了");
        }

        protected override void Build(NarrativeComposer n) {
            n.Say(NarrativeIds.Mayo, Line1.Value,
                    onEnter: HimayoNarrativePortrait.FaceEnter(HimayoFullBodyPortrait.Face.Ruminate))
             .Say(NarrativeIds.Mayo, Line2.Value,
                    onEnter: HimayoNarrativePortrait.FaceEnter(HimayoFullBodyPortrait.Face.Forsmile));
        }

        protected override void OnStarted() => HimayoNarrativePortrait.Show();

        protected override void OnCompleted() => HimayoNarrativePortrait.Hide();
    }
}

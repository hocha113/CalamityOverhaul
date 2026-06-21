using CalamityOverhaul.Content.Scenarios.Shepel;
using InnoVault.Narrative.Composition;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Shepel.Dialogues.Reactive
{
    internal sealed class ShepelRainDialogue : ShepelReactiveNarrative
    {
        protected override ShepelReactiveEvent HandledEvent => ShepelReactiveEvent.RainStarted;

        public static LocalizedText Line1 { get; private set; }
        public static LocalizedText Line2 { get; private set; }

        public override void SetStaticDefaults() {
            Line1 = this.GetLocalization(nameof(Line1),
                () => "开始降水了，这场雨看起来会持续一阵子。");
            Line2 = this.GetLocalization(nameof(Line2),
                () => "其实还挺安静的。如果战场允许的话。");
        }

        protected override void Build(NarrativeComposer n) {
            ConsumeEvent();
            n.Say("SHPC", Line1.Value)
             .Say("SHPC", Line2.Value);
        }
    }
}

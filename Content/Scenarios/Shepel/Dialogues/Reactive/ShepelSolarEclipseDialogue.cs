using InnoVault.Narrative.Composition;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Shepel.Dialogues.Reactive
{
    internal sealed class ShepelSolarEclipseDialogue : ShepelReactiveNarrative
    {
        protected override ShepelReactiveEvent HandledEvent => ShepelReactiveEvent.SolarEclipse;

        public static LocalizedText Line1 { get; private set; }
        public static LocalizedText Line2 { get; private set; }

        public override void SetStaticDefaults() {
            Line1 = this.GetLocalization(nameof(Line1),
                () => "日光消失，外界生物信号大规模涌现。日食事件，全线进入高戒备状态。");
            Line2 = this.GetLocalization(nameof(Line2),
                () => "保持移动，我会盯住一切。");
        }

        protected override void Build(NarrativeComposer n) {
            ConsumeEvent();
            n.Say("SHPC", Line1.Value, onEnter: PortraitSerious)
             .Say("SHPC", Line2.Value);
        }
    }
}

using CalamityOverhaul.Content.Narrative.Scenarios.Shepel;
using InnoVault.Narrative.Composition;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Narrative.Scenarios.Shepel.Dialogues.Reactive
{
    internal sealed class ShepelCyberLevelUpDialogue : ShepelReactiveNarrative
    {
        protected override ShepelReactiveEvent HandledEvent => ShepelReactiveEvent.CyberLevelUp;

        public static LocalizedText Line1 { get; private set; }
        public static LocalizedText Line2 { get; private set; }

        public override void SetStaticDefaults() {
            Line1 = this.GetLocalization(nameof(Line1),
                () => "领域层级提升。安全范围已扩大，扫描精度同步上升。");
            Line2 = this.GetLocalization(nameof(Line2),
                () => "我会将所有潜在的致命威胁，统统阻挡在您的视线之外。");
        }

        protected override void Build(NarrativeComposer n) {
            ConsumeEvent();
            n.Say("SHPC", Line1.Value)
             .Say("SHPC", Line2.Value);
        }
    }
}

using InnoVault.Narrative.Composition;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Shepel.Dialogues.Reactive.Bosses
{
    internal sealed class ShepelGolemDialogue : ShepelReactiveNarrative
    {
        protected override ShepelReactiveEvent HandledEvent => ShepelReactiveEvent.BossDefeated;
        protected override int TargetBossNpcType => NPCID.Golem;
        public static LocalizedText Line1 { get; private set; }
        public static LocalizedText Line2 { get; private set; }

        public override void SetStaticDefaults() {
            Line1 = this.GetLocalization(nameof(Line1),
                            () => "石巨人的核心已停止运转。虽然有着庞大沉重的身躯，但在主人的指引下，也不过是一堆易碎的石块。");
            Line2 = this.GetLocalization(nameof(Line2),
                () => "这种死板的防御机制，在我们的火力协同下毫无意义。");
        }

        protected override void Build(NarrativeComposer n) {
            ConsumeEvent();
            n
            .Say("Shepel", Line1.Value, onEnter: PortraitSerious)
            .Say("Shepel", Line2.Value, onEnter: PortraitSmirk);
        }
    }
}

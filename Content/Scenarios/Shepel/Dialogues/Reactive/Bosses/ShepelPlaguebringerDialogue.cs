using InnoVault.Narrative.Composition;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Shepel.Dialogues.Reactive.Bosses
{
    internal sealed class ShepelPlaguebringerDialogue : ShepelReactiveNarrative
    {
        protected override ShepelReactiveEvent HandledEvent => ShepelReactiveEvent.BossDefeated;
        protected override int TargetBossNpcType => CWRID.NPC_PlaguebringerGoliath;
        public static LocalizedText Line1 { get; private set; }
        public static LocalizedText Line2 { get; private set; }

        public override void SetStaticDefaults() {
            Line1 = this.GetLocalization(nameof(Line1),
                            () => "瘟疫使者已坠毁。请戴好过滤面具，残存的毒素污染将由我来全数净化。");
            Line2 = this.GetLocalization(nameof(Line2),
                () => "军事化的瘟疫载体……背后的设计痕迹十分严重。这件事我会深查到底。");
        }

        protected override void Build(NarrativeComposer n) {
            ConsumeEvent();
            n
            .Say("Shepel", Line1.Value, onEnter: PortraitSerious)
            .Say("Shepel", Line2.Value);
        }
    }
}

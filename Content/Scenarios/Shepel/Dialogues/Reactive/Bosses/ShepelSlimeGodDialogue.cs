using CalamityOverhaul.Content.Scenarios.Shepel;
using InnoVault.Narrative.Composition;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Shepel.Dialogues.Reactive.Bosses
{
    internal sealed class ShepelSlimeGodDialogue : ShepelReactiveNarrative
    {
        protected override ShepelReactiveEvent HandledEvent => ShepelReactiveEvent.BossDefeated;
        protected override int TargetBossNpcType => CWRID.NPC_SlimeGodCore;
        public static LocalizedText Line1 { get; private set; }
        public static LocalizedText Line2 { get; private set; }

        public override void SetStaticDefaults() {
Line1 = this.GetLocalization(nameof(Line1),
                () => "史莱姆之神的核心已溃散。周围散落着大量的凝胶残骸。");
            Line2 = this.GetLocalization(nameof(Line2),
                () => "请稍微等一下，让我为您清理掉护甲上沾染的污渍，保持您的整洁也是女仆的职责。");
        }

        protected override void Build(NarrativeComposer n) {
            ConsumeEvent();
             n
             .Say("Shepel", Line1.Value)
             .Say("Shepel", Line2.Value);
        }
    }
}

using InnoVault.Narrative.Composition;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ID;
using CalamityOverhaul.Content.Scenarios.Shepel;

namespace CalamityOverhaul.Content.Scenarios.Shepel.Dialogues.Reactive.Bosses
{
    internal sealed class ShepelQueenBeeDialogue : ShepelReactiveNarrative
    {
        protected override ShepelReactiveEvent HandledEvent => ShepelReactiveEvent.BossDefeated;
        protected override int TargetBossNpcType => NPCID.QueenBee;
        public static LocalizedText Line1 { get; private set; }
        public static LocalizedText Line2 { get; private set; }

        public override void SetStaticDefaults() {
Line1 = this.GetLocalization(nameof(Line1),
                () => "蜂群的中枢已被摧毁。失去指挥的毒蜂已经溃散。");
            Line2 = this.GetLocalization(nameof(Line2),
                () => "无论是有机的蜂群还是无机的机群，在主人的默契指令面前，都不足为惧。");
        }

        protected override void Build(NarrativeComposer n) {
            ConsumeEvent();
             n
             .Say("Shepel", Line1.Value)
             .Say("Shepel", Line2.Value);
        }
    }
}

using InnoVault.Narrative.Composition;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Shepel.Dialogues.Reactive.Bosses
{
    internal sealed class ShepelDestroyerDialogue : ShepelReactiveNarrative
    {
        protected override ShepelReactiveEvent HandledEvent => ShepelReactiveEvent.BossDefeated;
        protected override int TargetBossNpcType => NPCID.TheDestroyer;
        public static LocalizedText Line1 { get; private set; }
        public static LocalizedText Line2 { get; private set; }

        public override void SetStaticDefaults() {
            Line1 = this.GetLocalization(nameof(Line1),
                            () => "毁灭者的主控节点已被彻底摧毁。确认周边没有残留的激光威胁。");
            Line2 = this.GetLocalization(nameof(Line2),
                () => "不用在意它庞大的体型，在我的火力计算面前，它只是一堆待拆解的废铁。");
        }

        protected override void Build(NarrativeComposer n) {
            ConsumeEvent();
            n
            .Say("Shepel", Line1.Value)
            .Say("Shepel", Line2.Value);
        }
    }
}

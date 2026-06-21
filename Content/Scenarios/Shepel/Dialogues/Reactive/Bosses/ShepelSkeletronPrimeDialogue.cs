using InnoVault.Narrative.Composition;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Shepel.Dialogues.Reactive.Bosses
{
    internal sealed class ShepelSkeletronPrimeDialogue : ShepelReactiveNarrative
    {
        protected override ShepelReactiveEvent HandledEvent => ShepelReactiveEvent.BossDefeated;
        protected override int TargetBossNpcType => NPCID.SkeletronPrime;
        public static LocalizedText Line1 { get; private set; }
        public static LocalizedText Line2 { get; private set; }

        public override void SetStaticDefaults() {
            Line1 = this.GetLocalization(nameof(Line1),
                            () => "机械统帅已被拆解。事实证明，单纯堆砌火力和护甲毫无意义。");
            Line2 = this.GetLocalization(nameof(Line2),
                () => "它那种死板的家伙，永远无法匹敌您与我之间紧密相连的战术配合。");
        }

        protected override void Build(NarrativeComposer n) {
            ConsumeEvent();
            n
            .Say("Shepel", Line1.Value)
            .Say("Shepel", Line2.Value);
        }
    }
}

using InnoVault.Narrative.Composition;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Shepel.Dialogues.Reactive.Bosses
{
    internal sealed class ShepelBrimstoneElementalDialogue : ShepelReactiveNarrative
    {
        protected override ShepelReactiveEvent HandledEvent => ShepelReactiveEvent.BossDefeated;
        protected override int TargetBossNpcType => CWRID.NPC_BrimstoneElemental;
        public static LocalizedText Line1 { get; private set; }
        public static LocalizedText Line2 { get; private set; }

        public override void SetStaticDefaults() {
            Line1 = this.GetLocalization(nameof(Line1),
                            () => "硫磺火元素已被镇压。区域高温正在消散，请让我为您检查一下护甲的隔热层。");
            Line2 = this.GetLocalization(nameof(Line2),
                () => "这种纯粹的恶意与愤怒十分危险。但请放心，我会将所有的伤害都拦截下来。");
        }

        protected override void Build(NarrativeComposer n) {
            ConsumeEvent();
            n
            .Say("Shepel", Line1.Value, onEnter: PortraitSerious)
            .Say("Shepel", Line2.Value);
        }
    }
}

using InnoVault.Narrative.Composition;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Shepel.Dialogues.Reactive.Bosses
{
    internal sealed class ShepelProvidenceDialogue : ShepelReactiveNarrative
    {
        protected override ShepelReactiveEvent HandledEvent => ShepelReactiveEvent.BossDefeated;
        protected override int TargetBossNpcType => CWRID.NPC_Providence;
        public static LocalizedText Line1 { get; private set; }
        public static LocalizedText Line2 { get; private set; }
        public static LocalizedText Line3 { get; private set; }

        public override void SetStaticDefaults() {
            Line1 = this.GetLocalization(nameof(Line1),
                            () => "亵渎天神已陨落。刚才那股恐怖的光芒没有灼伤您吧？");
            Line2 = this.GetLocalization(nameof(Line2),
                () => "您战胜了神明级别的存在……我的主人。");
            Line3 = this.GetLocalization(nameof(Line3),
                () => "为了配得上现在的您，我需要将自己的机能改进得更完美才行。");
        }

        protected override void Build(NarrativeComposer n) {
            ConsumeEvent();
            n
            .Say("Shepel", Line1.Value, onEnter: PortraitShocked)
            .Say("Shepel", Line2.Value, onEnter: PortraitSerious)
            .Say("Shepel", Line3.Value);
        }
    }
}

using InnoVault.Narrative.Composition;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Shepel.Dialogues.Reactive
{
    //通用回落，低于各Boss专属
    internal sealed class ShepelBossDefeatedDialogue : ShepelReactiveNarrative
    {
        public override int DialoguePriority => 48;

        protected override ShepelReactiveEvent HandledEvent => ShepelReactiveEvent.BossDefeated;

        public static LocalizedText Line1 { get; private set; }
        public static LocalizedText Line2 { get; private set; }

        public override void SetStaticDefaults() {
            Line1 = this.GetLocalization(nameof(Line1),
                () => "目标已确认肃清。主人没有受伤吧？");
            Line2 = this.GetLocalization(nameof(Line2),
                () => "为您扫除前方的一切障碍，是我最大的荣幸。");
        }

        protected override void Build(NarrativeComposer n) {
            ConsumeEvent();
            n.Say("SHPC", Line1.Value, onEnter: PortraitSerious)
             .Say("SHPC", Line2.Value, onEnter: PortraitHappy);
        }
    }
}

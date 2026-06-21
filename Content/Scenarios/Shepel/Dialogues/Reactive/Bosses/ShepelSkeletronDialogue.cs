using InnoVault.Narrative.Composition;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ID;
using CalamityOverhaul.Content.Scenarios.Shepel;

namespace CalamityOverhaul.Content.Scenarios.Shepel.Dialogues.Reactive.Bosses
{
    internal sealed class ShepelSkeletronDialogue : ShepelReactiveNarrative
    {
        protected override ShepelReactiveEvent HandledEvent => ShepelReactiveEvent.BossDefeated;
        protected override int TargetBossNpcType => NPCID.SkeletronHead;
        public static LocalizedText Line1 { get; private set; }
        public static LocalizedText Line2 { get; private set; }

        public override void SetStaticDefaults() {
Line1 = this.GetLocalization(nameof(Line1),
                () => "地牢的守护者已被击败。外部屏障消散，通道已经安全了。");
            Line2 = this.GetLocalization(nameof(Line2),
                () => "前方区域缺乏照明，请允许我走在前面，为您照亮这条幽暗的道路。");
        }

        protected override void Build(NarrativeComposer n) {
            ConsumeEvent();
             n
             .Say("Shepel", Line1.Value)
             .Say("Shepel", Line2.Value);
        }
    }
}

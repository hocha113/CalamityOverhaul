using CalamityOverhaul.Content.Narrative.Scenarios.Shepel;
using InnoVault.Narrative.Composition;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ID;

namespace CalamityOverhaul.Content.Narrative.Scenarios.Shepel.Dialogues.Reactive.Bosses
{
    internal sealed class ShepelPlanteraDialogue : ShepelReactiveNarrative
    {
        protected override ShepelReactiveEvent HandledEvent => ShepelReactiveEvent.BossDefeated;
        protected override int TargetBossNpcType => NPCID.Plantera;
        public static LocalizedText Line1 { get; private set; }
        public static LocalizedText Line2 { get; private set; }

        public override void SetStaticDefaults() {
Line1 = this.GetLocalization(nameof(Line1),
                () => "世纪之花的生机已被切断。前方的道路已扫描完毕。");
            Line2 = this.GetLocalization(nameof(Line2),
                () => "请跟在我的身后，小心脚下残存的毒刺和荆棘。神庙的入口就在前方。");
        }

        protected override void Build(NarrativeComposer n) {
            ConsumeEvent();
             n
             .Say("Shepel", Line1.Value)
             .Say("Shepel", Line2.Value);
        }
    }
}

using InnoVault.Narrative.Composition;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Shepel.Dialogues.Reactive.Bosses
{
    internal sealed class ShepelDevourerofGodsDialogue : ShepelReactiveNarrative
    {
        protected override ShepelReactiveEvent HandledEvent => ShepelReactiveEvent.BossDefeated;
        protected override int TargetBossNpcType => CWRID.NPC_DevourerofGodsHead;
        public static LocalizedText Line1 { get; private set; }
        public static LocalizedText Line2 { get; private set; }
        public static LocalizedText Line3 { get; private set; }

        public override void SetStaticDefaults() {
            Line1 = this.GetLocalization(nameof(Line1),
                            () => "噬神者的维度轨迹已彻底消失……它以神明为食，是我记录中威胁等级最高的目标之一。");
            Line2 = this.GetLocalization(nameof(Line2),
                () => "您展现出的决断力远超我的预测模型。这份跨越生死的共同战斗数据，我会永远珍藏。");
            Line3 = this.GetLocalization(nameof(Line3),
                () => "请允许我，一直追随在如此耀眼的您的身边。");
        }

        protected override void Build(NarrativeComposer n) {
            ConsumeEvent();
            n
            .Say("Shepel", Line1.Value)
            .Say("Shepel", Line2.Value)
            .Say("Shepel", Line3.Value);
        }
    }
}

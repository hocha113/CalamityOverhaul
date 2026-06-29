using InnoVault.Narrative.Composition;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Shepel.Dialogues.Reactive
{
    internal sealed class ShepelBloodMoonDialogue : ShepelReactiveNarrative
    {
        protected override ShepelReactiveEvent HandledEvent => ShepelReactiveEvent.BloodMoon;

        public static LocalizedText Line1 { get; private set; }
        public static LocalizedText Line2 { get; private set; }

        public override void SetStaticDefaults() {
            Line1 = this.GetLocalization(nameof(Line1),
                () => "月球引力数据异常，夜间敌对生物密度急剧攀升。已确认为血月。");
            Line2 = this.GetLocalization(nameof(Line2),
                () => "传感器有些嘈杂，但我会一直守在此处。主人，留意脚下。");
        }

        protected override void Build(NarrativeComposer n) {
            ConsumeEvent();
            n.Say("SHPC", Line1.Value, onEnter: PortraitSerious)
             .Say("SHPC", Line2.Value);
        }
    }
}

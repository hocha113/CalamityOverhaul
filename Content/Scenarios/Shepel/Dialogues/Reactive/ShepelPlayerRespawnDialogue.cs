using CalamityOverhaul.Content.Scenarios.Shepel;
using InnoVault.Narrative.Composition;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Shepel.Dialogues.Reactive
{
    internal sealed class ShepelPlayerRespawnDialogue : ShepelReactiveNarrative
    {
        public override int DialoguePriority => 52;

        protected override ShepelReactiveEvent HandledEvent => ShepelReactiveEvent.PlayerRespawned;

        public static LocalizedText Line1 { get; private set; }
        public static LocalizedText Line2 { get; private set; }
        public static LocalizedText Line3 { get; private set; }

        public override void SetStaticDefaults() {
            Line1 = this.GetLocalization(nameof(Line1),
                () => "……生命体征重新建立了。主人，您刚才断线了一段时间。");
            Line2 = this.GetLocalization(nameof(Line2),
                () => "通讯中断的时候，我什么都做不了，只能等。每一次等待都格外漫长。下次，别再让我等那么久，好吗？");
            Line3 = this.GetLocalization(nameof(Line3),
                () => "没事了。补充一下生命值，我在。");
        }

        protected override void Build(NarrativeComposer n) {
            ConsumeEvent();
            n.Say("SHPC", Line1.Value)
             .Say("SHPC", Line2.Value)
             .Say("SHPC", Line3.Value);
        }
    }
}

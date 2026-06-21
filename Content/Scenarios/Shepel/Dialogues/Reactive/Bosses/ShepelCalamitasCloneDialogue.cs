using CalamityOverhaul.Content.Scenarios.Shepel;
using InnoVault.Narrative.Composition;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Shepel.Dialogues.Reactive.Bosses
{
    internal sealed class ShepelCalamitasCloneDialogue : ShepelReactiveNarrative
    {
        protected override ShepelReactiveEvent HandledEvent => ShepelReactiveEvent.BossDefeated;
        protected override int TargetBossNpcType => CWRID.NPC_CalamitasClone;
        public static LocalizedText Line1 { get; private set; }
        public static LocalizedText Line2 { get; private set; }
        public static LocalizedText Line3 { get; private set; }

        public override void SetStaticDefaults() {
Line1 = this.GetLocalization(nameof(Line1),
                () => "灾厄克隆体的信号已消失。主人没有被这股反射能量波及吧？");
            Line2 = this.GetLocalization(nameof(Line2),
                () => "扫描显示本体的威胁依然潜伏在深处……如果她敢出现。");
            Line3 = this.GetLocalization(nameof(Line3),
                () => "我会不惜一切代价挡在您身前。");
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

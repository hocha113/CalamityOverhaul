using InnoVault.Narrative.Composition;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Shepel.Dialogues.Reactive.Bosses
{
    internal sealed class ShepelMoonLordDialogue : ShepelReactiveNarrative
    {
        protected override ShepelReactiveEvent HandledEvent => ShepelReactiveEvent.BossDefeated;
        protected override int TargetBossNpcType => NPCID.MoonLordCore;
        public static LocalizedText Line1 { get; private set; }
        public static LocalizedText Line2 { get; private set; }
        public static LocalizedText Line3 { get; private set; }

        public override void SetStaticDefaults() {
            Line1 = this.GetLocalization(nameof(Line1),
                            () => "月亮领主的信号已经彻底消失。主人，这是我当前运行周期里记录过的最高威胁等级目标。");
            Line2 = this.GetLocalization(nameof(Line2),
                () => "笼罩在天空的压迫感消散了，但我不会放松警惕。");
            Line3 = this.GetLocalization(nameof(Line3),
                () => "无论这片星空下还隐藏着什么秘密，我都会紧握您的手，绝不退缩。");
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

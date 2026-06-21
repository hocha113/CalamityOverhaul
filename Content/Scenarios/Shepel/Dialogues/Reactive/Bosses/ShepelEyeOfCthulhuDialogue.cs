using InnoVault.Narrative.Composition;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ID;
using CalamityOverhaul.Content.Scenarios.Shepel;

namespace CalamityOverhaul.Content.Scenarios.Shepel.Dialogues.Reactive.Bosses
{
    internal sealed class ShepelEyeOfCthulhuDialogue : ShepelReactiveNarrative
    {
        protected override ShepelReactiveEvent HandledEvent => ShepelReactiveEvent.BossDefeated;
        protected override int TargetBossNpcType => NPCID.EyeofCthulhu;
        public static LocalizedText Line1 { get; private set; }
        public static LocalizedText Line2 { get; private set; }

        public override void SetStaticDefaults() {
Line1 = this.GetLocalization(nameof(Line1),
                () => "主人，首个威胁目标已被清除。这场战斗的数据我都记录好了。");
            Line2 = this.GetLocalization(nameof(Line2),
                () => "这种级别的威胁以后无需再放在心上，我将为主人扫除。");
        }

        protected override void Build(NarrativeComposer n) {
            ConsumeEvent();
             n
             .Say("Shepel", Line1.Value)
             .Say("Shepel", Line2.Value);
        }
    }
}

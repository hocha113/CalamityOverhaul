using InnoVault.Narrative.Composition;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Shepel.Dialogues.Reactive
{
    internal sealed class ShepelRAMOverloadDialogue : ShepelReactiveNarrative
    {
        protected override ShepelReactiveEvent HandledEvent => ShepelReactiveEvent.RAMOverload;

        public static LocalizedText Line1 { get; private set; }
        public static LocalizedText Line2 { get; private set; }

        public override void SetStaticDefaults() {
            Line1 = this.GetLocalization(nameof(Line1),
                () => "警告：RAM占用率已逼近临界值。");
            Line2 = this.GetLocalization(nameof(Line2),
                () => "主人，建议停止RAM高消耗操作，等待恢复到安全阈值。");
        }

        protected override void Build(NarrativeComposer n) {
            ConsumeEvent();
            n.Say("SHPC", Line1.Value, onEnter: PlayOverloadCue)
             .Say("SHPC", Line2.Value);
        }

        private static void PlayOverloadCue() {
            ShepelNarrativePortrait.SetFace(ShepelFullBodyPortrait.Face.Serious);
            ShepelNarrativePortrait.TriggerGlitch(0.5f, 0.4f);
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.MenuClose);
            }
        }
    }
}

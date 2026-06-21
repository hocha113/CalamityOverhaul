using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Narrative.Scenarios.Shepel;
using InnoVault.Narrative.Composition;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Narrative.Scenarios.Shepel.Dialogues.Reactive.Bosses
{
    internal sealed class ShepelSupremeCalamitasDialogue : ShepelReactiveNarrative
    {
        protected override ShepelReactiveEvent HandledEvent => ShepelReactiveEvent.BossDefeated;
        protected override int TargetBossNpcType => CWRID.NPC_SupremeCalamitas;
        public static LocalizedText Line1 { get; private set; }
        public static LocalizedText Line2 { get; private set; }
        public static LocalizedText Line3 { get; private set; }

        public override void SetStaticDefaults() {
            Line1 = this.GetLocalization(nameof(Line1),
                () => "真正的灾厄，确认终止。我的全域威胁监控显示：危险等级归零。");
            Line2 = this.GetLocalization(nameof(Line2),
                () => "主人……我不知道该如何准确表达此刻的感受。所有预测模型里最坏的结局都没有发生，因为您在这里。");
            Line3 = this.GetLocalization(nameof(Line3),
                () => "这段数据我会永久保存，不设覆写权限。");
        }

        protected override void Build(NarrativeComposer n) {
            ConsumeEvent();
            n.Say("SHPC", Line1.Value, onEnter: ShepelNarrativePortrait.FaceEnter(ShepelFullBodyPortrait.Face.Serious))
             .Say("SHPC", Line2.Value, onEnter: PlayLine2Cue)
             .Say("SHPC", Line3.Value);
        }

        private static void PlayLine2Cue() {
            ShepelNarrativePortrait.SetFace(ShepelFullBodyPortrait.Face.Happy);
            ShepelNarrativePortrait.TriggerGlitch(0.3f, 0.2f);
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.MenuTick);
            }
        }
    }
}

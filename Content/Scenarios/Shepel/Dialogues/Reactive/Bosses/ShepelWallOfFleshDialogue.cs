using InnoVault.Narrative.Composition;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Shepel.Dialogues.Reactive.Bosses
{
    internal sealed class ShepelWallOfFleshDialogue : ShepelReactiveNarrative
    {
        protected override ShepelReactiveEvent HandledEvent => ShepelReactiveEvent.BossDefeated;
        protected override int TargetBossNpcType => NPCID.WallofFlesh;
        public static LocalizedText Line1 { get; private set; }
        public static LocalizedText Line2 { get; private set; }
        public static LocalizedText Line3 { get; private set; }

        public override void SetStaticDefaults() {
            Line1 = this.GetLocalization(nameof(Line1),
                            () => "检测到世界底层规则正在重构。平衡已被打破，未知的威胁正在涌现。");
            Line2 = this.GetLocalization(nameof(Line2),
                () => "有更多可怕的东西正在挣脱束缚。主人……您会感到不安吗？");
            Line3 = this.GetLocalization(nameof(Line3),
                () => "请不必忧虑。无论世界如何异变，我都会死死守在您的身前。");
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

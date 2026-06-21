using InnoVault.Narrative.Composition;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Shepel.CybCourses
{
    internal sealed class CybCourseOutroDialogue : ShepelCybCourseDialogue
    {
        public override string LocalizationCategory => "ADV.Shepel";

        protected override bool SkipPortraitFadeInOnStart => true;

        public static LocalizedText SpeakerName { get; private set; }
        public static LocalizedText Line1 { get; private set; }
        public static LocalizedText Line2 { get; private set; }
        public static LocalizedText Line3 { get; private set; }

        public override void SetStaticDefaults() {
            SpeakerName = this.GetLocalization(nameof(SpeakerName), () => "SHPC");
            Line1 = this.GetLocalization(nameof(Line1),
                () => "训练完成。所有接口均已完成校准。");
            Line2 = this.GetLocalization(nameof(Line2),
                () => "您已掌握神经直连协议，超梦节点正在脱钩。");
            Line3 = this.GetLocalization(nameof(Line3),
                () => "随时可重置训练，或退出超梦。");
        }

        protected override void Build(NarrativeComposer n) {
            n.SayTimed("SHPC", Line1.Value, 4f)
             .SayTimed("SHPC", Line2.Value, 4.5f)
             .Say("SHPC", Line3.Value);
        }

        protected override void OnCourseCompleted() => CybCourseCompletePanel.Show();
    }
}

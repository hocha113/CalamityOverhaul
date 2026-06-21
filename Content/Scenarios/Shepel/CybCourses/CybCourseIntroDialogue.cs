using InnoVault.Narrative.Composition;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Shepel.CybCourses
{
    internal sealed class CybCourseIntroDialogue : ShepelCybCourseDialogue
    {
        public override string LocalizationCategory => "ADV.Shepel";

        public static LocalizedText SpeakerName { get; private set; }
        public static LocalizedText Line1 { get; private set; }
        public static LocalizedText Line2 { get; private set; }
        public static LocalizedText Line3 { get; private set; }
        public static LocalizedText Line4 { get; private set; }

        public override void SetStaticDefaults() {
            SpeakerName = this.GetLocalization(nameof(SpeakerName), () => "SHPC");
            Line1 = this.GetLocalization(nameof(Line1),
                () => "主人，欢迎进入神经训练节点。");
            Line2 = this.GetLocalization(nameof(Line2),
                () => "这里是为您准备的沉浸式教学空间。");
            Line3 = this.GetLocalization(nameof(Line3),
                () => "SHPC是一种强大的智能武器，您将学习如何操作它的HUD界面。");
            Line4 = this.GetLocalization(nameof(Line4),
                () => "准备好将意识与我连接了吗？请握紧我的手，我们开始吧。");
        }

        protected override void Build(NarrativeComposer n) {
            n.SayTimed("SHPC", Line1.Value, 4.5f)
             .SayTimed("SHPC", Line2.Value, 4.5f)
             .SayTimed("SHPC", Line3.Value, 6f)
             .Say("SHPC", Line4.Value);
        }

        protected override void OnCourseCompleted() => CybTutorialLead.BeginSHPCTutorial();
    }
}

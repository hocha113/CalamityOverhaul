using InnoVault.Narrative.Composition;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Shepel.CybCourses
{
    internal sealed class CybCourseHackIntroDialogue : ShepelCybCourseDialogue
    {
        public override string LocalizationCategory => "ADV.Shepel";

        public static LocalizedText SpeakerName { get; private set; }
        public static LocalizedText Line1 { get; private set; }
        public static LocalizedText Line2 { get; private set; }
        public static LocalizedText Line3 { get; private set; }

        public override void SetStaticDefaults() {
            SpeakerName = this.GetLocalization(nameof(SpeakerName), () => "SHPC");
            Line1 = this.GetLocalization(nameof(Line1),
                () => "接口解析完毕。下一项训练：骇客时间。");
            Line2 = this.GetLocalization(nameof(Line2),
                () => "骇客时间是SHPC专属的神经干预协议。激活后，外部时间流将冻结，您可以从容选择目标并上传定制骇入程序。默认按键是 {0}。");
            Line3 = this.GetLocalization(nameof(Line3),
                () => "前方的测试单元已固定就位。按下 {0} 进入骇客时间，然后点击锁定它。");
        }

        protected override void Build(NarrativeComposer n) {
            n.SayTimed("SHPC", Line1.Value, 4.5f)
             .SayTimed("SHPC", HackTimeTutorialLead.ResolveKeyTokens(Line2.Value), 6.5f)
             .Say("SHPC", HackTimeTutorialLead.ResolveKeyTokens(Line3.Value));
        }

        protected override void OnCourseCompleted() => HackTimeTutorialLead.BeginHackTimeTutorial();
    }
}

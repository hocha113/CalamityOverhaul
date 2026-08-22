using InnoVault.Narrative.Composition;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Shepel.CybCourses
{
    internal sealed class CybCourseWheelIntroDialogue : ShepelCybCourseDialogue
    {
        public override string LocalizationCategory => "ADV.Shepel";
        public static LocalizedText Line1 { get; private set; }
        public static LocalizedText Line2 { get; private set; }
        public static LocalizedText Line3 { get; private set; }

        public override void SetStaticDefaults() {
            Line1 = this.GetLocalization(nameof(Line1),
                () => "两项核心协议都已通过验证。最后，教您一个把它们收进指尖的手势。");
            Line2 = this.GetLocalization(nameof(Line2),
                () => "按住 {0}，快捷转盘就会展开，领域的三个层级排在盘面上，正中央就是骇客时间的入口。");
            Line3 = this.GetLocalization(nameof(Line3),
                () => "来吧。按住 {0}，把光标甩向想要的层级，松手即可。");
        }

        protected override void Build(NarrativeComposer n) {
            n.SayTimed("SHPC", Line1.Value, 4.5f)
             .SayTimed("SHPC", WheelTutorialLead.ResolveKeyTokens(Line2.Value), 6.5f)
             .Say("SHPC", WheelTutorialLead.ResolveKeyTokens(Line3.Value));
        }

        protected override void OnCourseCompleted() => WheelTutorialLead.BeginWheelTutorial();
    }
}

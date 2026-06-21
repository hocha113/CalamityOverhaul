using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Narrative.Presentation
{
    internal class DialogueSystem : ModSystem, ILocalizedModType
    {
        public string LocalizationCategory => "UI";
        public static LocalizedText ContinueHint { get; set; }
        public static LocalizedText FastHint { get; set; }
        public static LocalizedText AutoHint { get; set; }
        public static LocalizedText SkipHint { get; set; }
        public static LocalizedText ClaimHint { get; set; }
        public static LocalizedText PopupContinueHint { get; set; }
        public static LocalizedText ChoiceTitle { get; set; }

        public override void SetStaticDefaults() {
            ContinueHint = this.GetLocalization(nameof(ContinueHint), () => "继续");
            FastHint = this.GetLocalization(nameof(FastHint), () => "加速");
            AutoHint = this.GetLocalization(nameof(AutoHint), () => "自动");
            SkipHint = this.GetLocalization(nameof(SkipHint), () => "跳过");
            ClaimHint = this.GetLocalization(nameof(ClaimHint), () => "点击领取");
            PopupContinueHint = this.GetLocalization(nameof(PopupContinueHint), () => "点击继续");
            ChoiceTitle = this.GetLocalization(nameof(ChoiceTitle), () => "选择");
        }
    }
}

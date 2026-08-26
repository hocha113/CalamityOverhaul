using CalamityOverhaul.Content.Scenarios.Helen;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.SupCal
{
    internal sealed class SupCalDefeat : NarrativeScenario, ILocalizedModType
    {
        public string LocalizationCategory => "ADV.SupCal";
        public static LocalizedText Rolename2 { get; private set; }
        public static LocalizedText Line1 { get; private set; }
        public static LocalizedText Line2 { get; private set; }
        public static LocalizedText Line3 { get; private set; }
        public static LocalizedText Line4 { get; private set; }
        public static LocalizedText Line5 { get; private set; }
        public static LocalizedText Line6 { get; private set; }
        public static LocalizedText Line7 { get; private set; }
        public static LocalizedText Line8 { get; private set; }
        public static LocalizedText Line9 { get; private set; }
        public static LocalizedText Line10 { get; private set; }

        public override StyleId DefaultStyle => "Brimstone";

        public override void SetStaticDefaults() {
            Rolename2 = this.GetLocalization(nameof(Rolename2), () => "比目鱼&[Name]");
            Line1 = this.GetLocalization(nameof(Line1), () => "你竟然已经到达这种地步了吗......呵，是我技不如人了");
            Line2 = this.GetLocalization(nameof(Line2), () => "但你并非最强，你或许很不错，但那个人绝对不会比你差");
            Line3 = this.GetLocalization(nameof(Line3), () => "亚利姆已经走到了那条道路的尽头，到达了泰拉人的极致，没人会比他强");
            Line4 = this.GetLocalization(nameof(Line4), () => "可惜，你们不能见面......");
            Line5 = this.GetLocalization(nameof(Line5), () => "你的层次太低，永远无法理解我现在的状态");
            Line6 = this.GetLocalization(nameof(Line6), () => "......我层次太低?");
            Line7 = this.GetLocalization(nameof(Line7), () => "(活这么多年，还是第一次被说层次太低)");
            Line8 = this.GetLocalization(nameof(Line8), () => "我是一个时代孕育出来的唯一，既然敢舍弃泰拉人的身份，自封为神，自然是无所不能");
            Line9 = this.GetLocalization(nameof(Line9), () => "你说亚利姆可以称量我?得让克希洛克来");
            Line10 = this.GetLocalization(nameof(Line10), () => "......");
        }

        protected override void Build(NarrativeComposer n) {
            n.Say("SupCal", Line1.Value)
             .Say("SupCal", Line2.Value)
             .Say("SupCal", Line3.Value)
             .Say("SupCal", Line4.Value)
             .Say("HalibutPlayer", Line5.Value)
             .Say("SupCal", "CloseEye", Line6.Value)
             .Say("SupCal", "CloseEye", Line7.Value)
             .Say("HalibutPlayer", Line8.Value)
             .Say("HalibutPlayer", Line9.Value)
             .Say("SupCal", "CloseEye", Line10.Value);
        }

        protected override void OnCompleted() {
            HalibutStorySync.WriteSupCal(
                d => d.SupCalDefeat = true,
                d => d.SupCalDefeat = true);
        }
    }
}

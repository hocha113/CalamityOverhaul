using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend;
using CalamityOverhaul.Content.Narrative;
using CalamityOverhaul.Content.Scenarios.Himayo.ToriiShrines;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using InnoVault.Narrative.Runtime;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Himayo
{
    internal sealed class FirstMetHimayo : StoryScenario, ILocalizedModType
    {
        public string LocalizationCategory => "ADV.Himayo";

        public static LocalizedText SpeakerName { get; private set; }
        public static LocalizedText Line1 { get; private set; }
        public static LocalizedText Line2 { get; private set; }
        public static LocalizedText Line3 { get; private set; }
        public static LocalizedText Line4 { get; private set; }
        public static LocalizedText Line5 { get; private set; }
        public static LocalizedText Line6 { get; private set; }
        public static LocalizedText Line7 { get; private set; }

        public override StyleId DefaultStyle => NarrativeIds.Onikiri;

        public override void SetStaticDefaults() {
            SpeakerName = this.GetLocalization(nameof(SpeakerName), () => "绯村真夜");
            Line1 = this.GetLocalization(nameof(Line1), () => "……我还活着？");
            Line2 = this.GetLocalization(nameof(Line2), () => "等等，先确认一下……这是招魂吗？");
            Line3 = this.GetLocalization(nameof(Line3), () => "不对。");
            Line4 = this.GetLocalization(nameof(Line4), () => "为什么我的意识，现在在一把刀里面？");
            Line5 = this.GetLocalization(nameof(Line5), () => "头疼……我的状态大概介于活人和异类之间吧。");
            Line6 = this.GetLocalization(nameof(Line6), () => "完全搞不懂发生了什么……");
            Line7 = this.GetLocalization(nameof(Line7), () => "不过从今天开始，就请多多关照了。");
        }

        protected override void Build(NarrativeComposer n) {
            n.AllowSkipThrough()
             .Say(NarrativeIds.Mayo, Line1.Value, Voice[1],
                    onEnter: HimayoNarrativePortrait.FaceEnter(HimayoFullBodyPortrait.Face.Doubt))
             .Say(NarrativeIds.Mayo, Line2.Value, Voice[2],
                    onEnter: HimayoNarrativePortrait.FaceEnter(HimayoFullBodyPortrait.Face.Ruminate))
             .Say(NarrativeIds.Mayo, Line3.Value, Voice[3],
                    onEnter: HimayoNarrativePortrait.FaceEnter(HimayoFullBodyPortrait.Face.Doubt))
             .Say(NarrativeIds.Mayo, Line4.Value, Voice[4])
             .Say(NarrativeIds.Mayo, Line5.Value, Voice[5],
                    onEnter: HimayoNarrativePortrait.FaceEnter(HimayoFullBodyPortrait.Face.Ruminate))
             .Say(NarrativeIds.Mayo, Line6.Value, Voice[6],
                    onEnter: HimayoNarrativePortrait.FaceEnter(HimayoFullBodyPortrait.Face.Ruminate))
             .Say(NarrativeIds.Mayo, Line7.Value, Voice[7],
                    onEnter: HimayoNarrativePortrait.FaceEnter(HimayoFullBodyPortrait.Face.Forsmile))
             .End();
        }

        //拿到鬼切即触发，但要等鸟居退场演出（含余响后的静默拍）收完
        //目送容身的鸟居沉没之后，真夜才开口
        //切歌不在本脚本：黄昏起时 ToriiDuskMusicClaim 认领 Future，提示框跟认领首胜
        protected override NarrativePolicy ConfigurePolicy() => new() {
            IsCompleted = _ => HimayoStorySync.FirstMet,
            CanTrigger = (_, player) => player.HasItem(ModContent.ItemType<OnikiriItem>())
                && !ToriiShrineActor.DepartureHoldingStage,
            OnTriggered = _ => HimayoStorySync.MarkFirstMet(),
            //对话真正落幕才记「播完」：试炼委托以此为发放门禁
            OnCompleted = _ => HimayoStorySync.MarkPostFirstMetComplete(),
        };

        protected override void OnStarted() => HimayoNarrativePortrait.ShowPetalAssembly();

        protected override void OnCompleted() => HimayoNarrativePortrait.Hide();
    }
}

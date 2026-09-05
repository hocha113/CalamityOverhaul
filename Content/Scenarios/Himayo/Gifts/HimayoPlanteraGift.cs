using CalamityOverhaul.Content.Narrative;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Himayo.Gifts
{
    /// <summary>onikiri.011，A 剪败花，卖花口吻</summary>
    internal sealed class HimayoPlanteraGift : HimayoBossGiftNarrative, ILocalizedModType
    {
        public string LocalizationCategory => "ADV.Himayo";

        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }

        public override StyleId DefaultStyle => NarrativeIds.Onikiri;

        public override void SetStaticDefaults() {
            L0 = this.GetLocalization(nameof(L0), () => "呼，藤蔓总算枯下去了，一股烂根的土腥气");
            L1 = this.GetLocalization(nameof(L1), () => "我们花村常说，花要是烂了芯子，再舍不得也得连根拔除，不然整片花田都得遭殃");
            L2 = this.GetLocalization(nameof(L2), () => "枯枝败叶烂在地里也没什么看头，占地方还惹人心烦");
            L3 = this.GetLocalization(nameof(L3), () => "刀上缠的花苞碎叶拍一拍，收拾干净咱们就动身");
        }

        protected override void Build(NarrativeComposer n) {
            n.Say(NarrativeIds.Mayo, L0.Value, Voice[1], onEnter: PortraitFace(HimayoFullBodyPortrait.Face.Ruminate))
             .Say(NarrativeIds.Mayo, L1.Value, Voice[2])
             .Say(NarrativeIds.Mayo, L2.Value, Voice[3], onEnter: PortraitFace(HimayoFullBodyPortrait.Face.Forsmile))
             .GiftReward(GiftKey)
             .Say(NarrativeIds.Mayo, L3.Value, Voice[4], onEnter: PortraitFace(HimayoFullBodyPortrait.Face.Grin));
        }

        protected override bool IsGiftCompleted()
            => HimayoStorySync.ReadGift(d => d.PlanteraGift, d => d.PlanteraGift);

        protected override void MarkGiftCompleted()
            => HimayoStorySync.WriteGift(d => d.PlanteraGift = true, d => d.PlanteraGift = true);
    }
}

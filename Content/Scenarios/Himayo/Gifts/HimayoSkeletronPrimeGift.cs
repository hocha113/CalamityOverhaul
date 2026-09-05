using CalamityOverhaul.Content.Narrative;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Himayo.Gifts
{
    /// <summary>onikiri.009，B 头旧身新</summary>
    internal sealed class HimayoSkeletronPrimeGift : HimayoBossGiftNarrative, ILocalizedModType
    {
        public string LocalizationCategory => "ADV.Himayo";

        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }

        public override StyleId DefaultStyle => NarrativeIds.Onikiri;

        public override void SetStaticDefaults() {
            L0 = this.GetLocalization(nameof(L0), () => "顶着个老骷髅头，底下倒装了四把崭新的大铁锯子");
            L1 = this.GetLocalization(nameof(L1), () => "活像新衣服套了一半就火急火燎出门打架，看着别扭极了");
            L2 = this.GetLocalization(nameof(L2), () => "骨架全散一地了，趁那些铁臂还没凉透，找找有什么趁手的");
        }

        protected override void Build(NarrativeComposer n) {
            n.Say(NarrativeIds.Mayo, L0.Value, Voice[1], onEnter: PortraitFace(HimayoFullBodyPortrait.Face.Doubt))
             .Say(NarrativeIds.Mayo, L1.Value, Voice[2], onEnter: PortraitFace(HimayoFullBodyPortrait.Face.Grin))
             .GiftReward(GiftKey)
             .Say(NarrativeIds.Mayo, L2.Value, Voice[3], onEnter: PortraitFace(HimayoFullBodyPortrait.Face.Forsmile));
        }

        protected override bool IsGiftCompleted()
            => HimayoStorySync.ReadGift(d => d.SkeletronPrimeGift, d => d.SkeletronPrimeGift);

        protected override void MarkGiftCompleted()
            => HimayoStorySync.WriteGift(d => d.SkeletronPrimeGift = true, d => d.SkeletronPrimeGift = true);
    }
}

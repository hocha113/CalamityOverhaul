using CalamityOverhaul.Content.Narrative;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Himayo.Gifts
{
    /// <summary>onikiri.003，E 塞物不解释；黏作幽默/接受</summary>
    internal sealed class HimayoSlimeGodGift : HimayoBossGiftNarrative, ILocalizedModType
    {
        public string LocalizationCategory => "ADV.Himayo";

        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }

        public override StyleId DefaultStyle => NarrativeIds.Onikiri;

        public override void SetStaticDefaults() {
            L0 = this.GetLocalization(nameof(L0), () => "踩烂泥踩多了，倒觉得这团神仙浆子手感不算太糟");
            L1 = this.GetLocalization(nameof(L1), () => "起码没顺着裤腿往鞋帮里钻，算它还有点神仙样");
            L2 = this.GetLocalization(nameof(L2), () => "顺手从果冻堆里捞出来的，拿着吧，可别追问我是从哪儿找着的");
        }

        protected override void Build(NarrativeComposer n) {
            n.Say(NarrativeIds.Mayo, L0.Value, Voice[1], onEnter: PortraitFace(HimayoFullBodyPortrait.Face.Ruminate))
             .Say(NarrativeIds.Mayo, L1.Value, Voice[2], onEnter: PortraitFace(HimayoFullBodyPortrait.Face.Forsmile))
             .GiftReward(GiftKey)
             .Say(NarrativeIds.Mayo, L2.Value, Voice[3]);
        }

        protected override bool IsGiftCompleted()
            => HimayoStorySync.ReadGift(d => d.SlimeGodGift, d => d.SlimeGodGift);

        protected override void MarkGiftCompleted()
            => HimayoStorySync.WriteGift(d => d.SlimeGodGift = true, d => d.SlimeGodGift = true);
    }
}

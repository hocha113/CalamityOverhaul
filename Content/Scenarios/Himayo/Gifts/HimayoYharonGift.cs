using CalamityOverhaul.Content.Narrative;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Himayo.Gifts
{
    /// <summary>onikiri.018，F 刃烫+搁一边</summary>
    internal sealed class HimayoYharonGift : HimayoBossGiftNarrative, ILocalizedModType
    {
        public string LocalizationCategory => "ADV.Himayo";

        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }

        public override StyleId DefaultStyle => NarrativeIds.Onikiri;

        public override void SetStaticDefaults() {
            L0 = this.GetLocalization(nameof(L0), () => "刃还烫。我这边也热得慌");
            L1 = this.GetLocalization(nameof(L1), () => "说真的，我这边现在跟塞进烤箱差不多");
            L2 = this.GetLocalization(nameof(L2), () => "先别急着入鞘。烫着难受的是我");
            L3 = this.GetLocalization(nameof(L3), () => "这个，先搁一边。回头再说");
        }

        protected override void Build(NarrativeComposer n) {
            n.Say(NarrativeIds.Mayo, L0.Value, Voice[1], onEnter: PortraitFace(HimayoFullBodyPortrait.Face.Doubt))
             .Say(NarrativeIds.Mayo, L1.Value, Voice[2], onEnter: PortraitFace(HimayoFullBodyPortrait.Face.Forsmile))
             .Say(NarrativeIds.Mayo, L2.Value, Voice[3], onEnter: PortraitFace(HimayoFullBodyPortrait.Face.Ruminate))
             .GiftReward(GiftKey)
             .Say(NarrativeIds.Mayo, L3.Value, Voice[4], onEnter: PortraitFace(HimayoFullBodyPortrait.Face.Forsmile));
        }

        protected override bool IsGiftCompleted()
            => HimayoStorySync.ReadGift(d => d.YharonGift, d => d.YharonGift);

        protected override void MarkGiftCompleted()
            => HimayoStorySync.WriteGift(d => d.YharonGift = true, d => d.YharonGift = true);
    }
}

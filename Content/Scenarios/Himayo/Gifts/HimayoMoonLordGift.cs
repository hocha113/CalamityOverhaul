using CalamityOverhaul.Content.Narrative;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Himayo.Gifts
{
    /// <summary>onikiri.014，H 微痕：上面没了 + 站住吗</summary>
    internal sealed class HimayoMoonLordGift : HimayoBossGiftNarrative, ILocalizedModType
    {
        public string LocalizationCategory => "ADV.Himayo";

        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }

        public override StyleId DefaultStyle => NarrativeIds.Onikiri;

        public override void SetStaticDefaults() {
            L0 = this.GetLocalization(nameof(L0), () => "天顶上那片巨大的灰影……总算是彻底散尽了");
            L1 = this.GetLocalization(nameof(L1), () => "压了那么久的沉闷劲儿一下子空了，月光落下来，倒让人有点发懵");
            L2 = this.GetLocalization(nameof(L2), () => "还站得住吧？一路走到这儿真不容易，今晚可得好好睡上一整觉");
        }

        protected override void Build(NarrativeComposer n) {
            n.Say(NarrativeIds.Mayo, L0.Value, Voice[1], onEnter: PortraitFace(HimayoFullBodyPortrait.Face.Doubt))
             .Say(NarrativeIds.Mayo, L1.Value, Voice[2], onEnter: PortraitFace(HimayoFullBodyPortrait.Face.Ruminate))
             .GiftReward(GiftKey)
             .Say(NarrativeIds.Mayo, L2.Value, Voice[3]);
        }

        protected override bool IsGiftCompleted()
            => HimayoStorySync.ReadGift(d => d.MoonLordGift, d => d.MoonLordGift);

        protected override void MarkGiftCompleted()
            => HimayoStorySync.WriteGift(d => d.MoonLordGift = true, d => d.MoonLordGift = true);
    }
}

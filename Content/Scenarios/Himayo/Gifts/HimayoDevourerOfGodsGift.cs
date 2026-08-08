using CalamityOverhaul.Content.Narrative;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Himayo.Gifts
{
    /// <summary>onikiri.017，B 空/大</summary>
    internal sealed class HimayoDevourerOfGodsGift : HimayoBossGiftNarrative, ILocalizedModType
    {
        public string LocalizationCategory => "ADV.Himayo";

        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }

        public override StyleId DefaultStyle => NarrativeIds.Onikiri;

        public override void SetStaticDefaults() {
            L0 = this.GetLocalization(nameof(L0), () => "太大了。空得像把天挖掉了一块");
            L1 = this.GetLocalization(nameof(L1), () => "我盯着看了一会儿，自己也有点发愣");
            L2 = this.GetLocalization(nameof(L2), () => "别看太久。看久了心里也空……哎，我说完更像在吓人");
        }

        protected override void Build(NarrativeComposer n) {
            n.Say(NarrativeIds.Mayo, L0.Value, Voice[1], onEnter: PortraitFace(HimayoFullBodyPortrait.Face.Doubt))
             .Say(NarrativeIds.Mayo, L1.Value, Voice[2], onEnter: PortraitFace(HimayoFullBodyPortrait.Face.Ruminate))
             .GiftReward(GiftKey)
             .Say(NarrativeIds.Mayo, L2.Value, Voice[3], onEnter: PortraitFace(HimayoFullBodyPortrait.Face.Forsmile));
        }

        protected override bool IsGiftCompleted()
            => HimayoStorySync.ReadGift(d => d.DevourerOfGodsGift, d => d.DevourerOfGodsGift);

        protected override void MarkGiftCompleted()
            => HimayoStorySync.WriteGift(d => d.DevourerOfGodsGift = true, d => d.DevourerOfGodsGift = true);
    }
}

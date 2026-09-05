using CalamityOverhaul.Content.Narrative;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Himayo.Gifts
{
    /// <summary>onikiri.013，G 门口清静</summary>
    internal sealed class HimayoCultistGift : HimayoBossGiftNarrative, ILocalizedModType
    {
        public string LocalizationCategory => "ADV.Himayo";

        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }

        public override StyleId DefaultStyle => NarrativeIds.Onikiri;

        public override void SetStaticDefaults() {
            L0 = this.GetLocalization(nameof(L0), () => "呼……地牢门口总算彻底清静下来了");
            L1 = this.GetLocalization(nameof(L1), () => "刚才那一圈人神神叨叨念得没完没了，吵得我耳朵发胀，听得直心烦");
        }

        protected override void Build(NarrativeComposer n) {
            n.Say(NarrativeIds.Mayo, L0.Value, Voice[1], onEnter: PortraitFace(HimayoFullBodyPortrait.Face.Grin))
             .GiftReward(GiftKey)
             .Say(NarrativeIds.Mayo, L1.Value, Voice[2], onEnter: PortraitFace(HimayoFullBodyPortrait.Face.Ruminate));
        }

        protected override bool IsGiftCompleted()
            => HimayoStorySync.ReadGift(d => d.CultistGift, d => d.CultistGift);

        protected override void MarkGiftCompleted()
            => HimayoStorySync.WriteGift(d => d.CultistGift = true, d => d.CultistGift = true);
    }
}

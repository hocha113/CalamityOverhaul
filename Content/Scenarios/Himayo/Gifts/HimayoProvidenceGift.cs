using CalamityOverhaul.Content.Narrative;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Himayo.Gifts
{
    /// <summary>onikiri.015，E 气烫+塞物</summary>
    internal sealed class HimayoProvidenceGift : HimayoBossGiftNarrative, ILocalizedModType
    {
        public string LocalizationCategory => "ADV.Himayo";

        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }

        public override StyleId DefaultStyle => NarrativeIds.Onikiri;

        public override void SetStaticDefaults() {
            L0 = this.GetLocalization(nameof(L0), () => "嘶……热浪还没散，手背都烤得发红");
            L1 = this.GetLocalization(nameof(L1), () => "站在这儿跟蹲在刚出炉的灶膛边上烤番薯似的，头发丝都要焦了");
            L2 = this.GetLocalization(nameof(L2), () => "烫热的錾样我先给你挑出来了，拿稳了快撤，找个有水的地方凉快去");
        }

        protected override void Build(NarrativeComposer n) {
            n.Say(NarrativeIds.Mayo, L0.Value, Voice[1], onEnter: PortraitFace(HimayoFullBodyPortrait.Face.Doubt))
             .Say(NarrativeIds.Mayo, L1.Value, Voice[2], onEnter: PortraitFace(HimayoFullBodyPortrait.Face.Forsmile))
             .GiftReward(GiftKey)
             .Say(NarrativeIds.Mayo, L2.Value, Voice[3]);
        }

        protected override bool IsGiftCompleted()
            => HimayoStorySync.ReadGift(d => d.ProvidenceGift, d => d.ProvidenceGift);

        protected override void MarkGiftCompleted()
            => HimayoStorySync.WriteGift(d => d.ProvidenceGift = true, d => d.ProvidenceGift = true);
    }
}

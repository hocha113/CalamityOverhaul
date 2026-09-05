using CalamityOverhaul.Content.Narrative;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Himayo.Gifts
{
    /// <summary>onikiri.020，C 空气沉+清醒；接受一笔</summary>
    internal sealed class HimayoSupremeCalamitasGift : HimayoBossGiftNarrative, ILocalizedModType
    {
        public string LocalizationCategory => "ADV.Himayo";

        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }

        public override StyleId DefaultStyle => NarrativeIds.Onikiri;

        public override void SetStaticDefaults() {
            L0 = this.GetLocalization(nameof(L0), () => "结界碎了……那股缠得人透不过气的灰烬和压迫感，终于散开了");
            L1 = this.GetLocalization(nameof(L1), () => "别在风口里呆站着发木，深深吸口气，或者用冷水抹把脸醒醒神");
            L2 = this.GetLocalization(nameof(L2), () => "以前处理恶灵时这种九死一生的阵仗我见得多了……但这一次，有你在我身边");
        }

        protected override void Build(NarrativeComposer n) {
            n.Say(NarrativeIds.Mayo, L0.Value, Voice[1], onEnter: PortraitFace(HimayoFullBodyPortrait.Face.Ruminate))
             .Say(NarrativeIds.Mayo, L1.Value, Voice[2])
             .GiftReward(GiftKey)
             .Say(NarrativeIds.Mayo, L2.Value, Voice[3], onEnter: PortraitFace(HimayoFullBodyPortrait.Face.Forsmile));
        }

        protected override bool IsGiftCompleted()
            => HimayoStorySync.ReadGift(d => d.SupremeCalamitasGift, d => d.SupremeCalamitasGift);

        protected override void MarkGiftCompleted()
            => HimayoStorySync.WriteGift(d => d.SupremeCalamitasGift = true, d => d.SupremeCalamitasGift = true);
    }
}

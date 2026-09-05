using CalamityOverhaul.Content.Narrative;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Himayo.Gifts
{
    /// <summary>onikiri.000，D 跑题睡相小故事，无递物</summary>
    internal sealed class HimayoEyeOfCthulhuGift : HimayoBossGiftNarrative, ILocalizedModType
    {
        public string LocalizationCategory => "ADV.Himayo";

        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }
        public static LocalizedText L4 { get; private set; }

        public override StyleId DefaultStyle => NarrativeIds.Onikiri;

        public override void SetStaticDefaults() {
            L0 = this.GetLocalization(nameof(L0), () => "呼……总算落下来了，被那么大一只眼珠子盯了半宿，后背都直发毛");
            L1 = this.GetLocalization(nameof(L1), () => "不过这么一直干瞪着不眨，它自己到底嫌不嫌眼酸啊");
            L2 = this.GetLocalization(nameof(L2), () => "刚才被盯得久了，我自己都跟着有点犯困");
            L3 = this.GetLocalization(nameof(L3), () => "呐，在附近摸出来个小玩意，拿好了，别弄丢了");
            L4 = this.GetLocalization(nameof(L4), () => "歇会儿吧，今晚可算能踏实合眼了");
        }

        protected override void Build(NarrativeComposer n) {
            n.Say(NarrativeIds.Mayo, L0.Value, Voice[1], onEnter: PortraitFace(HimayoFullBodyPortrait.Face.Ruminate))
             .Say(NarrativeIds.Mayo, L1.Value, Voice[2])
             .Say(NarrativeIds.Mayo, L2.Value, Voice[3], onEnter: PortraitFace(HimayoFullBodyPortrait.Face.Doubt))
             .Say(NarrativeIds.Mayo, L3.Value, Voice[4], onEnter: PortraitFace(HimayoFullBodyPortrait.Face.Grin))
             .GiftReward(GiftKey)
             .Say(NarrativeIds.Mayo, L4.Value, Voice[5], onEnter: PortraitFace(HimayoFullBodyPortrait.Face.Ruminate));
        }

        protected override bool IsGiftCompleted()
            => HimayoStorySync.ReadGift(d => d.EyeOfCthulhuGift, d => d.EyeOfCthulhuGift);

        protected override void MarkGiftCompleted()
            => HimayoStorySync.WriteGift(d => d.EyeOfCthulhuGift = true, d => d.EyeOfCthulhuGift = true);
    }
}

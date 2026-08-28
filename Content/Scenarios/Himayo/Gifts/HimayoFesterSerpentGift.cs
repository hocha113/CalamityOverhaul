using CalamityOverhaul.Content.Narrative;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Himayo.Gifts
{
    /// <summary>onikiri.005，C 护刀+幽默；诊所笑话（接海鲜铺的旧梗）</summary>
    internal sealed class HimayoFesterSerpentGift : HimayoBossGiftNarrative, ILocalizedModType
    {
        public string LocalizationCategory => "ADV.Himayo";

        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }

        public override StyleId DefaultStyle => NarrativeIds.Onikiri;

        public override void SetStaticDefaults() {
            L0 = this.GetLocalization(nameof(L0), () => "等等。这回刃上的是……脓？");
            L1 = this.GetLocalization(nameof(L1), () => "我住里面啊。海鲜铺才关张，又改开诊所了");
            L2 = this.GetLocalization(nameof(L2), () => "还是没窗。病房连个通风口都没有");
            L3 = this.GetLocalization(nameof(L3), () => "下次挑干净点的地方下刀，我这儿实在晾不开");
        }

        protected override void Build(NarrativeComposer n) {
            n.Say(NarrativeIds.Mayo, L0.Value, Voice[1], onEnter: PortraitFace(HimayoFullBodyPortrait.Face.Doubt))
             .Say(NarrativeIds.Mayo, L1.Value, Voice[2], onEnter: PortraitFace(HimayoFullBodyPortrait.Face.Forsmile))
             .Say(NarrativeIds.Mayo, L2.Value, Voice[3], onEnter: PortraitFace(HimayoFullBodyPortrait.Face.Grin))
             .GiftReward(GiftKey)
             .Say(NarrativeIds.Mayo, L3.Value, Voice[4], onEnter: PortraitFace(HimayoFullBodyPortrait.Face.Forsmile));
        }

        protected override bool IsGiftCompleted()
            => HimayoStorySync.ReadGift(d => d.FesterSerpentGift, d => d.FesterSerpentGift);

        protected override void MarkGiftCompleted()
            => HimayoStorySync.WriteGift(d => d.FesterSerpentGift = true, d => d.FesterSerpentGift = true);
    }
}

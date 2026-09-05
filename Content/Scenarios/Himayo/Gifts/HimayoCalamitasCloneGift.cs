using CalamityOverhaul.Content.Narrative;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Himayo.Gifts
{
    /// <summary>onikiri.010，E 烦模仿+塞物</summary>
    internal sealed class HimayoCalamitasCloneGift : HimayoBossGiftNarrative, ILocalizedModType
    {
        public string LocalizationCategory => "ADV.Himayo";

        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }

        public override StyleId DefaultStyle => NarrativeIds.Onikiri;

        public override void SetStaticDefaults() {
            L0 = this.GetLocalization(nameof(L0), () => "呼……总算散了，飘在空中的那个影子，动作僵硬得像具被线吊着的木偶");
            L1 = this.GetLocalization(nameof(L1), () => "越是刻意学活人，反而越透着一股死板的假意，看着直让人膈应");
            L2 = this.GetLocalization(nameof(L2), () => "灰烬里落了片拓本，拿着吧，这回真不是假的了");
        }

        protected override void Build(NarrativeComposer n) {
            n.Say(NarrativeIds.Mayo, L0.Value, Voice[1], onEnter: PortraitFace(HimayoFullBodyPortrait.Face.Doubt))
             .Say(NarrativeIds.Mayo, L1.Value, Voice[2], onEnter: PortraitFace(HimayoFullBodyPortrait.Face.Ruminate))
             .GiftReward(GiftKey)
             .Say(NarrativeIds.Mayo, L2.Value, Voice[3], onEnter: PortraitFace(HimayoFullBodyPortrait.Face.Forsmile));
        }

        protected override bool IsGiftCompleted()
            => HimayoStorySync.ReadGift(d => d.CalamitasCloneGift, d => d.CalamitasCloneGift);

        protected override void MarkGiftCompleted()
            => HimayoStorySync.WriteGift(d => d.CalamitasCloneGift = true, d => d.CalamitasCloneGift = true);
    }
}

using CalamityOverhaul.Content.Narrative;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Himayo.Gifts
{
    /// <summary>onikiri.012，C 叮人+护刀</summary>
    internal sealed class HimayoGolemGift : HimayoBossGiftNarrative, ILocalizedModType
    {
        public string LocalizationCategory => "ADV.Himayo";

        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }

        public override StyleId DefaultStyle => NarrativeIds.Onikiri;

        public override void SetStaticDefaults() {
            L0 = this.GetLocalization(nameof(L0), () => "手腕酸不酸？快揉一揉，那大石墩子硬邦邦的，砍上去震得胳膊生疼");
            L1 = this.GetLocalization(nameof(L1), () => "刀锋险些都崩出豁口来了，我住里头都感觉一阵地动山摇");
            L2 = this.GetLocalization(nameof(L2), () => "往后对付这种硬疙瘩，多找找破绽，别老实巴交拿刃硬磕呀");
            L3 = this.GetLocalization(nameof(L3), () => "刀要是崩坏了我可饶不了你，快坐下缓口气吧");
        }

        protected override void Build(NarrativeComposer n) {
            n.Say(NarrativeIds.Mayo, L0.Value, Voice[1], onEnter: PortraitFace(HimayoFullBodyPortrait.Face.Ruminate))
             .Say(NarrativeIds.Mayo, L1.Value, Voice[2])
             .Say(NarrativeIds.Mayo, L2.Value, Voice[3])
             .GiftReward(GiftKey)
             .Say(NarrativeIds.Mayo, L3.Value, Voice[4], onEnter: PortraitFace(HimayoFullBodyPortrait.Face.Forsmile));
        }

        protected override bool IsGiftCompleted()
            => HimayoStorySync.ReadGift(d => d.GolemGift, d => d.GolemGift);

        protected override void MarkGiftCompleted()
            => HimayoStorySync.WriteGift(d => d.GolemGift = true, d => d.GolemGift = true);
    }
}

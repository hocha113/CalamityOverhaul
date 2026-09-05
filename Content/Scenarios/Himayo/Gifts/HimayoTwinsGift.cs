using CalamityOverhaul.Content.Narrative;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Himayo.Gifts
{
    /// <summary>onikiri.008，D 跑题对视笑场</summary>
    internal sealed class HimayoTwinsGift : HimayoBossGiftNarrative, ILocalizedModType
    {
        public string LocalizationCategory => "ADV.Himayo";

        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }

        public override StyleId DefaultStyle => NarrativeIds.Onikiri;

        public override void SetStaticDefaults() {
            L0 = this.GetLocalization(nameof(L0), () => "一只红的一只绿的，满天乱晃，眼珠子瞪得跟铜铃似的");
            L1 = this.GetLocalization(nameof(L1), () => "刚才打到一半我都在想，要是两只撞在一块儿，会不会把彼此给看晕了");
            L2 = this.GetLocalization(nameof(L2), () => "我以前跟人比赛谁先憋不住笑，每次都是我输");
            L3 = this.GetLocalization(nameof(L3), () => "噗……光是想想它俩撞在一起的场面，我都忍不住乐");
        }

        protected override void Build(NarrativeComposer n) {
            n.Say(NarrativeIds.Mayo, L0.Value, Voice[1], onEnter: PortraitFace(HimayoFullBodyPortrait.Face.Grin))
             .Say(NarrativeIds.Mayo, L1.Value, Voice[2])
             .Say(NarrativeIds.Mayo, L2.Value, Voice[3], onEnter: PortraitFace(HimayoFullBodyPortrait.Face.Forsmile))
             .GiftReward(GiftKey)
             .Say(NarrativeIds.Mayo, L3.Value, Voice[4], onEnter: PortraitFace(HimayoFullBodyPortrait.Face.Grin));
        }

        protected override bool IsGiftCompleted()
            => HimayoStorySync.ReadGift(d => d.TwinsGift, d => d.TwinsGift);

        protected override void MarkGiftCompleted()
            => HimayoStorySync.WriteGift(d => d.TwinsGift = true, d => d.TwinsGift = true);
    }
}

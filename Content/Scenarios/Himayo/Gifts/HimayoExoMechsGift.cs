using CalamityOverhaul.Content.Narrative;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Himayo.Gifts
{
    /// <summary>onikiri.019，A 耳嗡，碎嘴关心</summary>
    internal sealed class HimayoExoMechsGift : HimayoBossGiftNarrative, ILocalizedModType
    {
        public string LocalizationCategory => "ADV.Himayo";

        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }

        public override StyleId DefaultStyle => NarrativeIds.Onikiri;

        public override void SetStaticDefaults() {
            L0 = this.GetLocalization(nameof(L0), () => "哐当啷一片巨响总算熄火了，刚才炮火连天轰得我耳朵到现在还嗡嗡乱响");
            L1 = this.GetLocalization(nameof(L1), () => "简直像有小铁锤在脑壳里叮叮当当敲个没完，烦死人了");
            L2 = this.GetLocalization(nameof(L2), () => "咱们快离开这片全是焦铁味的地方，找个安静的山谷好好缓一缓");
        }

        protected override void Build(NarrativeComposer n) {
            n.Say(NarrativeIds.Mayo, L0.Value, Voice[1], onEnter: PortraitFace(HimayoFullBodyPortrait.Face.Ruminate))
             .Say(NarrativeIds.Mayo, L1.Value, Voice[2], onEnter: PortraitFace(HimayoFullBodyPortrait.Face.Doubt))
             .GiftReward(GiftKey)
             .Say(NarrativeIds.Mayo, L2.Value, Voice[3], onEnter: PortraitFace(HimayoFullBodyPortrait.Face.Forsmile));
        }

        protected override bool IsGiftCompleted()
            => HimayoStorySync.ReadGift(d => d.ExoMechsGift, d => d.ExoMechsGift);

        protected override void MarkGiftCompleted()
            => HimayoStorySync.WriteGift(d => d.ExoMechsGift = true, d => d.ExoMechsGift = true);
    }
}

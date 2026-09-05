using CalamityOverhaul.Content.Narrative;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Himayo.Gifts
{
    /// <summary>onikiri.007，F 贫名字+弱提</summary>
    internal sealed class HimayoDestroyerGift : HimayoBossGiftNarrative, ILocalizedModType
    {
        public string LocalizationCategory => "ADV.Himayo";

        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }

        public override StyleId DefaultStyle => NarrativeIds.Onikiri;

        public override void SetStaticDefaults() {
            L0 = this.GetLocalization(nameof(L0), () => "轰隆隆叫得震天响，拆开了一看，还真就是一长串大实心铁轱辘");
            L1 = this.GetLocalization(nameof(L1), () => "震得我脑壳到现在还在嗡嗡响呢");
            L2 = this.GetLocalization(nameof(L2), () => "铁芯里翻出来个硬家伙，拿稳了，掉地上砸着脚我可不背你");
        }

        protected override void Build(NarrativeComposer n) {
            n.Say(NarrativeIds.Mayo, L0.Value, Voice[1], onEnter: PortraitFace(HimayoFullBodyPortrait.Face.Grin))
             .Say(NarrativeIds.Mayo, L1.Value, Voice[2], onEnter: PortraitFace(HimayoFullBodyPortrait.Face.Forsmile))
             .GiftReward(GiftKey)
             .Say(NarrativeIds.Mayo, L2.Value, Voice[3]);
        }

        protected override bool IsGiftCompleted()
            => HimayoStorySync.ReadGift(d => d.DestroyerGift, d => d.DestroyerGift);

        protected override void MarkGiftCompleted()
            => HimayoStorySync.WriteGift(d => d.DestroyerGift = true, d => d.DestroyerGift = true);
    }
}

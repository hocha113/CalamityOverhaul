using CalamityOverhaul.Content.Narrative;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Himayo.Gifts
{
    /// <summary>onikiri.016，D 潮的生活联想，非霉洁癖</summary>
    internal sealed class HimayoPolterghastGift : HimayoBossGiftNarrative, ILocalizedModType
    {
        public string LocalizationCategory => "ADV.Himayo";

        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }

        public override StyleId DefaultStyle => NarrativeIds.Onikiri;

        public override void SetStaticDefaults() {
            L0 = this.GetLocalization(nameof(L0), () => "地底这片阴风吹得人骨头缝里都发潮，像屋里晾了半个月没干透的湿布");
            L1 = this.GetLocalization(nameof(L1), () => "连扇透气的窗都没有，生个炭盆也没柴火，在这儿睡一觉明天腰肯定酸疼");
            L2 = this.GetLocalization(nameof(L2), () => "冤魂全被荡平了，不过冷气还在往衣服里钻");
            L3 = this.GetLocalization(nameof(L3), () => "别在这阴冷坑里多呆，回地面上烤烤火，免得落一身湿寒");
        }

        protected override void Build(NarrativeComposer n) {
            n.Say(NarrativeIds.Mayo, L0.Value, Voice[1], onEnter: PortraitFace(HimayoFullBodyPortrait.Face.Ruminate))
             .Say(NarrativeIds.Mayo, L1.Value, Voice[2])
             .Say(NarrativeIds.Mayo, L2.Value, Voice[3], onEnter: PortraitFace(HimayoFullBodyPortrait.Face.Forsmile))
             .GiftReward(GiftKey)
             .Say(NarrativeIds.Mayo, L3.Value, Voice[4], onEnter: PortraitFace(HimayoFullBodyPortrait.Face.Ruminate));
        }

        protected override bool IsGiftCompleted()
            => HimayoStorySync.ReadGift(d => d.PolterghastGift, d => d.PolterghastGift);

        protected override void MarkGiftCompleted()
            => HimayoStorySync.WriteGift(d => d.PolterghastGift = true, d => d.PolterghastGift = true);
    }
}

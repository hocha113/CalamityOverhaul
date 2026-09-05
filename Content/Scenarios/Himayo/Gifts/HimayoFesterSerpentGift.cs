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
            L0 = this.GetLocalization(nameof(L0), () => "等等，这回刃上刮下来的又是黄沙又是脓水的，什么怪味道");
            L1 = this.GetLocalization(nameof(L1), () => "我住刀里连扇窗都没有，快赶上烂泥塘了");
            L2 = this.GetLocalization(nameof(L2), () => "稍微给我留点干净空气嘛，这儿实在晾不开");
            L3 = this.GetLocalization(nameof(L3), () => "下次挑个清爽点的家伙打嘛，哪怕是只硬壳蟹也比大烂虫强呀");
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

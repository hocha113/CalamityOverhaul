using CalamityOverhaul.Content.Narrative;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Himayo.Gifts
{
    /// <summary>onikiri.002，A 跑题+碎嘴关心；非洁癖</summary>
    internal sealed class HimayoCalamityEvilGift : HimayoBossGiftNarrative, ILocalizedModType
    {
        public string LocalizationCategory => "ADV.Himayo";

        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }
        public static LocalizedText L4 { get; private set; }

        public override StyleId DefaultStyle => NarrativeIds.Onikiri;

        public override void SetStaticDefaults() {
            L0 = this.GetLocalization(nameof(L0), () => "刚才那团乱七八糟挤在一块儿的……瞧着居然有点像捣烂的红豆沙");
            L1 = this.GetLocalization(nameof(L1), () => "咳，本来还不觉得，随口一说倒把自己说饿了");
            L2 = this.GetLocalization(nameof(L2), () => "你要是也饿了就去找点吃的，刚打完硬撑着最傻了");
            L3 = this.GetLocalization(nameof(L3), () => "顺手摸出来的小物件塞给你，拿着吧");
            L4 = this.GetLocalization(nameof(L4), () => "还有，手上有脏污别往脸上抹，抹完眼睛可真的会辣，听我的准没错");
        }

        protected override void Build(NarrativeComposer n) {
            n.Say(NarrativeIds.Mayo, L0.Value, Voice[1], onEnter: PortraitFace(HimayoFullBodyPortrait.Face.Ruminate))
             .Say(NarrativeIds.Mayo, L1.Value, Voice[2], onEnter: PortraitFace(HimayoFullBodyPortrait.Face.Forsmile))
             .Say(NarrativeIds.Mayo, L2.Value, Voice[3])
             .Say(NarrativeIds.Mayo, L3.Value, Voice[4], onEnter: PortraitFace(HimayoFullBodyPortrait.Face.Ruminate))
             .GiftReward(GiftKey)
             .Say(NarrativeIds.Mayo, L4.Value, Voice[5], onEnter: PortraitFace(HimayoFullBodyPortrait.Face.Forsmile));
        }

        protected override bool IsGiftCompleted()
            => HimayoStorySync.ReadGift(d => d.CalamityEvilGift, d => d.CalamityEvilGift);

        protected override void MarkGiftCompleted()
            => HimayoStorySync.WriteGift(d => d.CalamityEvilGift = true, d => d.CalamityEvilGift = true);
    }
}

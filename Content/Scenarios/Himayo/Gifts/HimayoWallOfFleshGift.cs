using CalamityOverhaul.Content.Narrative;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Himayo.Gifts
{
    /// <summary>onikiri.004，H 关系微痕；天气式提醒，不问安</summary>
    internal sealed class HimayoWallOfFleshGift : HimayoBossGiftNarrative, ILocalizedModType
    {
        public string LocalizationCategory => "ADV.Himayo";

        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }

        public override StyleId DefaultStyle => NarrativeIds.Onikiri;

        public override void SetStaticDefaults() {
            L0 = this.GetLocalization(nameof(L0), () => "……真给它劈开了，整堵肉墙都塌了");
            L1 = this.GetLocalization(nameof(L1), () => "怎么说呢，空气里的动静一下子全变了，连刀里的煞气都重了一截");
            L2 = this.GetLocalization(nameof(L2), () => "往后的天地大概更难应付了……不过嘛，握紧刀，有我陪着你呢");
        }

        protected override void Build(NarrativeComposer n) {
            n.Say(NarrativeIds.Mayo, L0.Value, Voice[1], onEnter: PortraitFace(HimayoFullBodyPortrait.Face.Doubt))
             .Say(NarrativeIds.Mayo, L1.Value, Voice[2], onEnter: PortraitFace(HimayoFullBodyPortrait.Face.Ruminate))
             .GiftReward(GiftKey)
             .Say(NarrativeIds.Mayo, L2.Value, Voice[3]);
        }

        protected override bool IsGiftCompleted()
            => HimayoStorySync.ReadGift(d => d.WallOfFleshGift, d => d.WallOfFleshGift);

        protected override void MarkGiftCompleted()
            => HimayoStorySync.WriteGift(d => d.WallOfFleshGift = true, d => d.WallOfFleshGift = true);
    }
}

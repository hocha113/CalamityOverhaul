using CalamityOverhaul.Content.Narrative;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Shenyo.Gifts
{
    /// <summary>kikasa.007：血肉墙</summary>
    internal sealed class ShenyoWallOfFleshGift : ShenyoBossGiftNarrative, ILocalizedModType
    {
        public string LocalizationCategory => "ADV.Shenyo";

        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }

        public override StyleId DefaultStyle => NarrativeIds.Kikasa;

        public override void SetStaticDefaults() {
            L0 = this.GetLocalization(nameof(L0), () => "地狱里那面肉墙，我隔着伞都嫌烫");
            L1 = this.GetLocalization(nameof(L1), () => "捅穿了，世界也就跟着变了，这种事你倒是干得挺熟练");
            L2 = this.GetLocalization(nameof(L2), () => "以后这种阵仗，还有的是");
        }

        protected override void Build(NarrativeComposer n) {
            n.Say(NarrativeIds.Shenyo, L0.Value, Voice[1], onEnter: PortraitFace(ShenyoFullBodyPortrait.Face.Wry))
             .Say(NarrativeIds.Shenyo, L1.Value, Voice[2], onEnter: PortraitFace(ShenyoFullBodyPortrait.Face.Scrutiny))
             .GiftTalisman(GiftId)
             .Say(NarrativeIds.Shenyo, L2.Value, Voice[3], onEnter: PortraitFace(ShenyoFullBodyPortrait.Face.Calm));
        }

        protected override bool IsGiftCompleted() => ShenyoStorySync.GiftStory.WallOfFleshGift;

        protected override void MarkGiftCompleted() => ShenyoStorySync.GiftStory.WallOfFleshGift = true;
    }
}

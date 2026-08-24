using CalamityOverhaul.Content.Narrative;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Shenyo.Gifts
{
    /// <summary>kikasa.008：史莱姆皇后</summary>
    internal sealed class ShenyoQueenSlimeGift : ShenyoBossGiftNarrative, ILocalizedModType
    {
        public string LocalizationCategory => "ADV.Shenyo";

        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }

        public override StyleId DefaultStyle => NarrativeIds.Kikasa;

        public override void SetStaticDefaults() {
            L0 = this.GetLocalization(nameof(L0), () => "粉粉嫩嫩的，倒是会打扮");
            L1 = this.GetLocalization(nameof(L1), () => "还带着侍卫，排场不小");
            L2 = this.GetLocalization(nameof(L2), () => "排场再大，也经不起一场雨");
        }

        protected override void Build(NarrativeComposer n) {
            n.Say(NarrativeIds.Shenyo, L0.Value, Voice[1], onEnter: PortraitFace(ShenyoFullBodyPortrait.Face.Wry))
             .Say(NarrativeIds.Shenyo, L1.Value, Voice[2], onEnter: PortraitFace(ShenyoFullBodyPortrait.Face.Scrutiny))
             .GiftTalisman(GiftId)
             .Say(NarrativeIds.Shenyo, L2.Value, Voice[3], onEnter: PortraitFace(ShenyoFullBodyPortrait.Face.Smile));
        }

        protected override bool IsGiftCompleted() => ShenyoStorySync.GiftStory.QueenSlimeGift;

        protected override void MarkGiftCompleted() => ShenyoStorySync.GiftStory.QueenSlimeGift = true;
    }
}

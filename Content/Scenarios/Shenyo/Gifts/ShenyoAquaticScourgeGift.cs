using CalamityOverhaul.Content.Narrative;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Shenyo.Gifts
{
    /// <summary>kikasa.009：渊海灾虫</summary>
    internal sealed class ShenyoAquaticScourgeGift : ShenyoBossGiftNarrative, ILocalizedModType
    {
        public string LocalizationCategory => "ADV.Shenyo";

        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }

        public override StyleId DefaultStyle => NarrativeIds.Kikasa;

        public override void SetStaticDefaults() {
            L0 = this.GetLocalization(nameof(L0), () => "水里泡烂的东西，我最看不惯");
            L1 = this.GetLocalization(nameof(L1), () => "那滩脏水，倒是比我的雨还难闻");
            L2 = this.GetLocalization(nameof(L2), () => "收拾完了把伞收一收，别沾着那股味回来");
        }

        protected override void Build(NarrativeComposer n) {
            n.Say(NarrativeIds.Shenyo, L0.Value, Voice[1], onEnter: PortraitFace(ShenyoFullBodyPortrait.Face.Lidded))
             .Say(NarrativeIds.Shenyo, L1.Value, Voice[2], onEnter: PortraitFace(ShenyoFullBodyPortrait.Face.Scrutiny))
             .GiftTalisman(GiftId)
             .Say(NarrativeIds.Shenyo, L2.Value, Voice[3], onEnter: PortraitFace(ShenyoFullBodyPortrait.Face.Wry));
        }

        protected override bool IsGiftCompleted() => ShenyoStorySync.GiftStory.AquaticScourgeGift;

        protected override void MarkGiftCompleted() => ShenyoStorySync.GiftStory.AquaticScourgeGift = true;
    }
}

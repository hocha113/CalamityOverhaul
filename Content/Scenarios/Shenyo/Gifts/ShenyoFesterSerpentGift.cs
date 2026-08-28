using CalamityOverhaul.Content.Narrative;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Shenyo.Gifts
{
    /// <summary>kikasa.009：脓蕾沙蟒</summary>
    internal sealed class ShenyoFesterSerpentGift : ShenyoBossGiftNarrative, ILocalizedModType
    {
        public string LocalizationCategory => "ADV.Shenyo";

        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }

        public override StyleId DefaultStyle => NarrativeIds.Kikasa;

        public override void SetStaticDefaults() {
            L0 = this.GetLocalization(nameof(L0), () => "沙子里烂出来的东西，比水里泡烂的还难看");
            L1 = this.GetLocalization(nameof(L1), () => "那身金脓不是蜜，凑近闻一口你就明白了");
            L2 = this.GetLocalization(nameof(L2), () => "办完把伞面冲一冲，脓渍干了就刮不掉了");
        }

        protected override void Build(NarrativeComposer n) {
            n.Say(NarrativeIds.Shenyo, L0.Value, Voice[1], onEnter: PortraitFace(ShenyoFullBodyPortrait.Face.Lidded))
             .Say(NarrativeIds.Shenyo, L1.Value, Voice[2], onEnter: PortraitFace(ShenyoFullBodyPortrait.Face.Scrutiny))
             .GiftTalisman(GiftId)
             .Say(NarrativeIds.Shenyo, L2.Value, Voice[3], onEnter: PortraitFace(ShenyoFullBodyPortrait.Face.Wry));
        }

        protected override bool IsGiftCompleted() => ShenyoStorySync.GiftStory.FesterSerpentGift;

        protected override void MarkGiftCompleted() => ShenyoStorySync.GiftStory.FesterSerpentGift = true;
    }
}

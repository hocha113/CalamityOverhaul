using CalamityOverhaul.Content.Narrative;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Shenyo.Gifts
{
    /// <summary>kikasa.018：幽海灵魂</summary>
    internal sealed class ShenyoPolterghastGift : ShenyoBossGiftNarrative, ILocalizedModType
    {
        public string LocalizationCategory => "ADV.Shenyo";

        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }

        public override StyleId DefaultStyle => NarrativeIds.Kikasa;

        public override void SetStaticDefaults() {
            L0 = this.GetLocalization(nameof(L0), () => "地牢里那团魂，缝缝补补好几百年");
            L1 = this.GetLocalization(nameof(L1), () => "跟我算是同类，你倒不必替它说情");
            L2 = this.GetLocalization(nameof(L2), () => "散了也好，好过在那儿吊着");
        }

        protected override void Build(NarrativeComposer n) {
            n.Say(NarrativeIds.Shenyo, L0.Value, Voice[1], onEnter: PortraitFace(ShenyoFullBodyPortrait.Face.Pensive))
             .Say(NarrativeIds.Shenyo, L1.Value, Voice[2], onEnter: PortraitFace(ShenyoFullBodyPortrait.Face.Wry))
             .Say(NarrativeIds.Shenyo, L2.Value, Voice[3], onEnter: PortraitFace(ShenyoFullBodyPortrait.Face.Lidded));
        }

        protected override bool IsGiftCompleted() => ShenyoStorySync.GiftStory.PolterghastGift;

        protected override void MarkGiftCompleted() => ShenyoStorySync.GiftStory.PolterghastGift = true;
    }
}

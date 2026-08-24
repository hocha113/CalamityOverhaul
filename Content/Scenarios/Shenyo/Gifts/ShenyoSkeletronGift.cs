using CalamityOverhaul.Content.Narrative;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Shenyo.Gifts
{
    /// <summary>kikasa.005：守门的骷髅王</summary>
    internal sealed class ShenyoSkeletronGift : ShenyoBossGiftNarrative, ILocalizedModType
    {
        public string LocalizationCategory => "ADV.Shenyo";

        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }

        public override StyleId DefaultStyle => NarrativeIds.Kikasa;

        public override void SetStaticDefaults() {
            L0 = this.GetLocalization(nameof(L0), () => "守门守了这么多年，骨头都该酥了");
            L1 = this.GetLocalization(nameof(L1), () => "换了我，早就懒得守了");
            L2 = this.GetLocalization(nameof(L2), () => "地牢的门开了，进去仔细些，别学它守株待兔守出瘾来");
        }

        protected override void Build(NarrativeComposer n) {
            n.Say(NarrativeIds.Shenyo, L0.Value, Voice[1], onEnter: PortraitFace(ShenyoFullBodyPortrait.Face.Pensive))
             .Say(NarrativeIds.Shenyo, L1.Value, Voice[2], onEnter: PortraitFace(ShenyoFullBodyPortrait.Face.Wry))
             .GiftTalisman(GiftId)
             .Say(NarrativeIds.Shenyo, L2.Value, Voice[3], onEnter: PortraitFace(ShenyoFullBodyPortrait.Face.Calm));
        }

        protected override bool IsGiftCompleted() => ShenyoStorySync.GiftStory.SkeletronGift;

        protected override void MarkGiftCompleted() => ShenyoStorySync.GiftStory.SkeletronGift = true;
    }
}

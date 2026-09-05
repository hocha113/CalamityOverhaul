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
            L0 = this.GetLocalization(nameof(L0), () => "地牢深处那些碎魂，缝缝补补吊了好几百年");
            L1 = this.GetLocalization(nameof(L1), () => "追究起来同我也算同类，倒用不着替它可惜");
            L2 = this.GetLocalization(nameof(L2), () => "散在水里归于清静也好，省得总在那儿哀哀戚戚");
        }

        protected override void Build(NarrativeComposer n) {
            n.Say(NarrativeIds.Shenyo, L0.Value, Voice[1], onEnter: PortraitFace(ShenyoFullBodyPortrait.Face.Pensive))
             .Say(NarrativeIds.Shenyo, L1.Value, Voice[2], onEnter: PortraitFace(ShenyoFullBodyPortrait.Face.Wry))
             .GiftTalisman(GiftId)
             .Say(NarrativeIds.Shenyo, L2.Value, Voice[3], onEnter: PortraitFace(ShenyoFullBodyPortrait.Face.Lidded));
        }

        protected override bool IsGiftCompleted() => ShenyoStorySync.GiftStory.PolterghastGift;

        protected override void MarkGiftCompleted() => ShenyoStorySync.GiftStory.PolterghastGift = true;
    }
}

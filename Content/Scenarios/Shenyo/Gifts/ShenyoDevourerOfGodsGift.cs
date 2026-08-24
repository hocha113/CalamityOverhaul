using CalamityOverhaul.Content.Narrative;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Shenyo.Gifts
{
    /// <summary>kikasa.020：神明吞噬者</summary>
    internal sealed class ShenyoDevourerOfGodsGift : ShenyoBossGiftNarrative, ILocalizedModType
    {
        public string LocalizationCategory => "ADV.Shenyo";

        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }

        public override StyleId DefaultStyle => NarrativeIds.Kikasa;

        public override void SetStaticDefaults() {
            L0 = this.GetLocalization(nameof(L0), () => "天上绕圈的那条，听说吞过神");
            L1 = this.GetLocalization(nameof(L1), () => "神是什么味道，我倒是有点好奇");
            L2 = this.GetLocalization(nameof(L2), () => "壳留着也好，肉不必留给我");
        }

        protected override void Build(NarrativeComposer n) {
            n.Say(NarrativeIds.Shenyo, L0.Value, Voice[1], onEnter: PortraitFace(ShenyoFullBodyPortrait.Face.Scrutiny))
             .Say(NarrativeIds.Shenyo, L1.Value, Voice[2], onEnter: PortraitFace(ShenyoFullBodyPortrait.Face.Murmur))
             .GiftTalisman(GiftId)
             .Say(NarrativeIds.Shenyo, L2.Value, Voice[3], onEnter: PortraitFace(ShenyoFullBodyPortrait.Face.Wry));
        }

        protected override bool IsGiftCompleted() => ShenyoStorySync.GiftStory.DevourerOfGodsGift;

        protected override void MarkGiftCompleted() => ShenyoStorySync.GiftStory.DevourerOfGodsGift = true;
    }
}

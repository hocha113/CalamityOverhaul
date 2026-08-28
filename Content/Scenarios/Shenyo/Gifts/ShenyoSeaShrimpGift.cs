using CalamityOverhaul.Content.Narrative;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Shenyo.Gifts
{
    /// <summary>kikasa.013：渊晶海虾</summary>
    internal sealed class ShenyoSeaShrimpGift : ShenyoBossGiftNarrative, ILocalizedModType
    {
        public string LocalizationCategory => "ADV.Shenyo";

        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }

        public override StyleId DefaultStyle => NarrativeIds.Kikasa;

        public override void SetStaticDefaults() {
            L0 = this.GetLocalization(nameof(L0), () => "海滩底下那只虾，一身晶壳，一拳能把水打空");
            L1 = this.GetLocalization(nameof(L1), () => "壳蜕了一层又一层，脾气倒是半点没软");
            L2 = this.GetLocalization(nameof(L2), () => "空响听着吓人，你接住了，手上算有真功夫");
        }

        protected override void Build(NarrativeComposer n) {
            n.Say(NarrativeIds.Shenyo, L0.Value, Voice[1], onEnter: PortraitFace(ShenyoFullBodyPortrait.Face.Scrutiny))
             .Say(NarrativeIds.Shenyo, L1.Value, Voice[2], onEnter: PortraitFace(ShenyoFullBodyPortrait.Face.Wry))
             .GiftTalisman(GiftId)
             .Say(NarrativeIds.Shenyo, L2.Value, Voice[3], onEnter: PortraitFace(ShenyoFullBodyPortrait.Face.Smile));
        }

        protected override bool IsGiftCompleted() => ShenyoStorySync.GiftStory.SeaShrimpGift;

        protected override void MarkGiftCompleted() => ShenyoStorySync.GiftStory.SeaShrimpGift = true;
    }
}

using CalamityOverhaul.Content.Narrative;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Shenyo.Gifts
{
    /// <summary>kikasa.003：腐巢/血肉宿主</summary>
    internal sealed class ShenyoCalamityEvilGift : ShenyoBossGiftNarrative, ILocalizedModType
    {
        public string LocalizationCategory => "ADV.Shenyo";

        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }

        public override StyleId DefaultStyle => NarrativeIds.Kikasa;

        public override void SetStaticDefaults() {
            L0 = this.GetLocalization(nameof(L0), () => "一窝一窝的，钻血又钻肉，看着都疼");
            L1 = this.GetLocalization(nameof(L1), () => "你倒是不怕疼");
            L2 = this.GetLocalization(nameof(L2), () => "下次当心点，我这把伞挡雨，挡不了那些虫子");
        }

        protected override void Build(NarrativeComposer n) {
            n.Say(NarrativeIds.Shenyo, L0.Value, Voice[1], onEnter: PortraitFace(ShenyoFullBodyPortrait.Face.Scrutiny))
             .Say(NarrativeIds.Shenyo, L1.Value, Voice[2], onEnter: PortraitFace(ShenyoFullBodyPortrait.Face.Murmur))
             .Say(NarrativeIds.Shenyo, L2.Value, Voice[3], onEnter: PortraitFace(ShenyoFullBodyPortrait.Face.Calm));
        }

        protected override bool IsGiftCompleted() => ShenyoStorySync.GiftStory.CalamityEvilGift;

        protected override void MarkGiftCompleted() => ShenyoStorySync.GiftStory.CalamityEvilGift = true;
    }
}

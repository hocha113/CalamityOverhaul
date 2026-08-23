using CalamityOverhaul.Content.Narrative;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Shenyo.Gifts
{
    /// <summary>kikasa.001：克苏鲁之眼</summary>
    internal sealed class ShenyoEyeOfCthulhuGift : ShenyoBossGiftNarrative, ILocalizedModType
    {
        public string LocalizationCategory => "ADV.Shenyo";

        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }

        public override StyleId DefaultStyle => NarrativeIds.Kikasa;

        public override void SetStaticDefaults() {
            L0 = this.GetLocalization(nameof(L0), () => "夜里睁着一只眼睛到处看，倒是不怕吓着自己");
            L1 = this.GetLocalization(nameof(L1), () => "眨都不眨，看得人心里发毛");
            L2 = this.GetLocalization(nameof(L2), () => "闭上了，也好，眼不见心不烦");
        }

        protected override void Build(NarrativeComposer n) {
            n.Say(NarrativeIds.Shenyo, L0.Value, Voice[1], onEnter: PortraitFace(ShenyoFullBodyPortrait.Face.Scrutiny))
             .Say(NarrativeIds.Shenyo, L1.Value, Voice[2], onEnter: PortraitFace(ShenyoFullBodyPortrait.Face.Murmur))
             .Say(NarrativeIds.Shenyo, L2.Value, Voice[3], onEnter: PortraitFace(ShenyoFullBodyPortrait.Face.Lidded));
        }

        protected override bool IsGiftCompleted() => ShenyoStorySync.GiftStory.EyeOfCthulhuGift;

        protected override void MarkGiftCompleted() => ShenyoStorySync.GiftStory.EyeOfCthulhuGift = true;
    }
}

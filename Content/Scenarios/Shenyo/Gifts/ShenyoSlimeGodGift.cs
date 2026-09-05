using CalamityOverhaul.Content.Narrative;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Shenyo.Gifts
{
    /// <summary>kikasa.006：史莱姆之神</summary>
    internal sealed class ShenyoSlimeGodGift : ShenyoBossGiftNarrative, ILocalizedModType
    {
        public string LocalizationCategory => "ADV.Shenyo";

        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }

        public override StyleId DefaultStyle => NarrativeIds.Kikasa;

        public override void SetStaticDefaults() {
            L0 = this.GetLocalization(nameof(L0), () => "红的紫的糊成一锅，也敢在嘴上称神");
            L1 = this.GetLocalization(nameof(L1), () => "能把这许多杂乱东西裹作一团，倒算有些层次");
            L2 = this.GetLocalization(nameof(L2), () => "化干净了就好，省得脏了眼前地方");
        }

        protected override void Build(NarrativeComposer n) {
            n.Say(NarrativeIds.Shenyo, L0.Value, Voice[1], onEnter: PortraitFace(ShenyoFullBodyPortrait.Face.Scrutiny))
             .Say(NarrativeIds.Shenyo, L1.Value, Voice[2], onEnter: PortraitFace(ShenyoFullBodyPortrait.Face.Wry))
             .GiftTalisman(GiftId)
             .Say(NarrativeIds.Shenyo, L2.Value, Voice[3], onEnter: PortraitFace(ShenyoFullBodyPortrait.Face.Lidded));
        }

        protected override bool IsGiftCompleted() => ShenyoStorySync.GiftStory.SlimeGodGift;

        protected override void MarkGiftCompleted() => ShenyoStorySync.GiftStory.SlimeGodGift = true;
    }
}

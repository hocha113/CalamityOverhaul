using CalamityOverhaul.Content.Narrative;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Shenyo.Gifts
{
    /// <summary>kikasa.014：猪鲨</summary>
    internal sealed class ShenyoDukeFishronGift : ShenyoBossGiftNarrative, ILocalizedModType
    {
        public string LocalizationCategory => "ADV.Shenyo";

        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }

        public override StyleId DefaultStyle => NarrativeIds.Kikasa;

        public override void SetStaticDefaults() {
            L0 = this.GetLocalization(nameof(L0), () => "一头猪也学人掀浪，也是奇了");
            L1 = this.GetLocalization(nameof(L1), () => "水的脾气，轮不到它使");
            L2 = this.GetLocalization(nameof(L2), () => "按回水里也好，省得它总在我眼皮底下晃");
        }

        protected override void Build(NarrativeComposer n) {
            n.Say(NarrativeIds.Shenyo, L0.Value, Voice[1], onEnter: PortraitFace(ShenyoFullBodyPortrait.Face.Wry))
             .Say(NarrativeIds.Shenyo, L1.Value, Voice[2], onEnter: PortraitFace(ShenyoFullBodyPortrait.Face.Scrutiny))
             .Say(NarrativeIds.Shenyo, L2.Value, Voice[3], onEnter: PortraitFace(ShenyoFullBodyPortrait.Face.Lidded));
        }

        protected override bool IsGiftCompleted() => ShenyoStorySync.GiftStory.DukeFishronGift;

        protected override void MarkGiftCompleted() => ShenyoStorySync.GiftStory.DukeFishronGift = true;
    }
}

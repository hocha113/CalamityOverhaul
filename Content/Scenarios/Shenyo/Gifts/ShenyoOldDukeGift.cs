using CalamityOverhaul.Content.Narrative;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Shenyo.Gifts
{
    /// <summary>kikasa.019：老公爵</summary>
    internal sealed class ShenyoOldDukeGift : ShenyoBossGiftNarrative, ILocalizedModType
    {
        public string LocalizationCategory => "ADV.Shenyo";

        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }

        public override StyleId DefaultStyle => NarrativeIds.Kikasa;

        public override void SetStaticDefaults() {
            L0 = this.GetLocalization(nameof(L0), () => "硫海底下那老东西，脾气比酸水还酸");
            L1 = this.GetLocalization(nameof(L1), () => "跟我打过几次照面，谁也没让过谁");
            L2 = this.GetLocalization(nameof(L2), () => "这回是你赢了，替我记他一笔");
        }

        protected override void Build(NarrativeComposer n) {
            n.Say(NarrativeIds.Shenyo, L0.Value, Voice[1], onEnter: PortraitFace(ShenyoFullBodyPortrait.Face.Scrutiny))
             .Say(NarrativeIds.Shenyo, L1.Value, Voice[2], onEnter: PortraitFace(ShenyoFullBodyPortrait.Face.Pensive))
             .GiftTalisman(GiftId)
             .Say(NarrativeIds.Shenyo, L2.Value, Voice[3], onEnter: PortraitFace(ShenyoFullBodyPortrait.Face.Smile));
        }

        protected override bool IsGiftCompleted() => ShenyoStorySync.GiftStory.OldDukeGift;

        protected override void MarkGiftCompleted() => ShenyoStorySync.GiftStory.OldDukeGift = true;
    }
}

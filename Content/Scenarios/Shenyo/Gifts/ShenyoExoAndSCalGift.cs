using CalamityOverhaul.Content.Narrative;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Shenyo.Gifts
{
    /// <summary>kikasa.022，星流巨械+至尊灾厄合一关，两组都要完成才通过</summary>
    internal sealed class ShenyoExoAndSCalGift : ShenyoBossGiftNarrative, ILocalizedModType
    {
        public string LocalizationCategory => "ADV.Shenyo";

        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }

        public override StyleId DefaultStyle => NarrativeIds.Kikasa;

        public override void SetStaticDefaults() {
            L0 = this.GetLocalization(nameof(L0), () => "工匠的得意，魔女的执念，凑一块倒是般配");
            L1 = this.GetLocalization(nameof(L1), () => "两桌都收了，算你今天没白忙");
            L2 = this.GetLocalization(nameof(L2), () => "往后这样的日子，不会再有几回了");
        }

        protected override void Build(NarrativeComposer n) {
            n.Say(NarrativeIds.Shenyo, L0.Value, Voice[1], onEnter: PortraitFace(ShenyoFullBodyPortrait.Face.Wry))
             .Say(NarrativeIds.Shenyo, L1.Value, Voice[2], onEnter: PortraitFace(ShenyoFullBodyPortrait.Face.Scrutiny))
             .GiftTalisman(GiftId)
             .Say(NarrativeIds.Shenyo, L2.Value, Voice[3], onEnter: PortraitFace(ShenyoFullBodyPortrait.Face.Pensive));
        }

        protected override bool IsGiftCompleted() => ShenyoStorySync.GiftStory.ExoAndSCalGift;

        protected override void MarkGiftCompleted() => ShenyoStorySync.GiftStory.ExoAndSCalGift = true;
    }
}

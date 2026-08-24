using CalamityOverhaul.Content.Narrative;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Shenyo.Gifts
{
    /// <summary>kikasa.010，三机械合一关：毁灭者+双子魔像+机械骷髅王，三者都要完成才通过</summary>
    internal sealed class ShenyoMechsGift : ShenyoBossGiftNarrative, ILocalizedModType
    {
        public string LocalizationCategory => "ADV.Shenyo";

        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }

        public override StyleId DefaultStyle => NarrativeIds.Kikasa;

        public override void SetStaticDefaults() {
            L0 = this.GetLocalization(nameof(L0), () => "链子、灯泡、骨架，凑一块倒像个笑话");
            L1 = this.GetLocalization(nameof(L1), () => "铁腥味重，我不爱，你倒是不嫌");
            L2 = this.GetLocalization(nameof(L2), () => "三件一起收拾，算你有耐心");
        }

        protected override void Build(NarrativeComposer n) {
            n.Say(NarrativeIds.Shenyo, L0.Value, Voice[1], onEnter: PortraitFace(ShenyoFullBodyPortrait.Face.Wry))
             .Say(NarrativeIds.Shenyo, L1.Value, Voice[2], onEnter: PortraitFace(ShenyoFullBodyPortrait.Face.Scrutiny))
             .GiftTalisman(GiftId)
             .Say(NarrativeIds.Shenyo, L2.Value, Voice[3], onEnter: PortraitFace(ShenyoFullBodyPortrait.Face.Calm));
        }

        protected override bool IsGiftCompleted() => ShenyoStorySync.GiftStory.MechsGift;

        protected override void MarkGiftCompleted() => ShenyoStorySync.GiftStory.MechsGift = true;
    }
}

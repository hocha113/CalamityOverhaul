using CalamityOverhaul.Content.Narrative;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Shenyo.Gifts
{
    /// <summary>kikasa.023，沉宴终章：BossRush或始源妖龙任一收官都算</summary>
    internal sealed class ShenyoBossRushGift : ShenyoBossGiftNarrative, ILocalizedModType
    {
        public string LocalizationCategory => "ADV.Shenyo";

        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }

        public override StyleId DefaultStyle => NarrativeIds.Kikasa;

        public override void SetStaticDefaults() {
            L0 = this.GetLocalization(nameof(L0), () => "都回来了，还是深渊里那条老的醒了？");
            L1 = this.GetLocalization(nameof(L1), () => "不管是哪种，这一趟都不轻松");
            L2 = this.GetLocalization(nameof(L2), () => "站稳了，别在我面前晃");
            L3 = this.GetLocalization(nameof(L3), () => "这顿吃完，雨也该停了");
        }

        protected override void Build(NarrativeComposer n) {
            n.Say(NarrativeIds.Shenyo, L0.Value, Voice[1], onEnter: PortraitFace(ShenyoFullBodyPortrait.Face.Scrutiny))
             .Say(NarrativeIds.Shenyo, L1.Value, Voice[2], onEnter: PortraitFace(ShenyoFullBodyPortrait.Face.Calm))
             .Say(NarrativeIds.Shenyo, L2.Value, Voice[3], onEnter: PortraitFace(ShenyoFullBodyPortrait.Face.Wry))
             .Say(NarrativeIds.Shenyo, L3.Value, Voice[4], onEnter: PortraitFace(ShenyoFullBodyPortrait.Face.Pensive));
        }

        protected override bool IsGiftCompleted() => ShenyoStorySync.GiftStory.BossRushGift;

        protected override void MarkGiftCompleted() => ShenyoStorySync.GiftStory.BossRushGift = true;
    }
}

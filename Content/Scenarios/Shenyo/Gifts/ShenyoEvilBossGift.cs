using CalamityOverhaul.Content.Narrative;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Shenyo.Gifts
{
    /// <summary>kikasa.002，世吞/克脑共位，按击杀分支换台词</summary>
    internal sealed class ShenyoEvilBossGift : ShenyoBossGiftNarrative, ILocalizedModType
    {
        public string LocalizationCategory => "ADV.Shenyo";

        public static LocalizedText L0Worm { get; private set; }
        public static LocalizedText L1Worm { get; private set; }
        public static LocalizedText L0Brain { get; private set; }
        public static LocalizedText L1Brain { get; private set; }
        public static LocalizedText L2 { get; private set; }

        public override StyleId DefaultStyle => NarrativeIds.Kikasa;

        public override void SetStaticDefaults() {
            L0Worm = this.GetLocalization(nameof(L0Worm), () => "土里钻出来一条，一节一节的，恶心");
            L1Worm = this.GetLocalization(nameof(L1Worm), () => "腥气比雨水还重，我倒是不嫌，你受得住就好");
            L0Brain = this.GetLocalization(nameof(L0Brain), () => "脑子摊在外头，也不嫌凉");
            L1Brain = this.GetLocalization(nameof(L1Brain), () => "这种东西，多看一眼都嫌脏了眼睛");
            L2 = this.GetLocalization(nameof(L2), () => "了结了就好，别沾一身腥气回来找我");
        }

        protected override void Build(NarrativeComposer n) {
            bool brain = ShenyoGiftNarrativeTracker.LastDefeatedBossId(GiftId) == NPCID.BrainofCthulhu;
            n.Say(NarrativeIds.Shenyo, brain ? L0Brain.Value : L0Worm.Value, brain ? Voice[3] : Voice[1],
                    onEnter: PortraitFace(ShenyoFullBodyPortrait.Face.Wry))
             .Say(NarrativeIds.Shenyo, brain ? L1Brain.Value : L1Worm.Value, brain ? Voice[4] : Voice[2],
                    onEnter: PortraitFace(ShenyoFullBodyPortrait.Face.Lidded))
             //台词分支只换文本，节点图仍是单线，两条路径都会经过这一次发放
             .GiftTalisman(GiftId)
             .Say(NarrativeIds.Shenyo, L2.Value, Voice[5], onEnter: PortraitFace(ShenyoFullBodyPortrait.Face.Smile));
        }

        protected override bool IsGiftCompleted() => ShenyoStorySync.GiftStory.EvilBossGift;

        protected override void MarkGiftCompleted() => ShenyoStorySync.GiftStory.EvilBossGift = true;
    }
}

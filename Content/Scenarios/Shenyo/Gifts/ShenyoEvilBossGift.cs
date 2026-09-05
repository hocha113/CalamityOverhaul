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
            L0Worm = this.GetLocalization(nameof(L0Worm), () => "泥里钻出来的一截截长虫，腥气重得难闻");
            L1Worm = this.GetLocalization(nameof(L1Worm), () => "比死水还沉的气味，你倒受得住");
            L0Brain = this.GetLocalization(nameof(L0Brain), () => "脑子就该好好待在骨头里，摊在外头也不嫌凉");
            L1Brain = this.GetLocalization(nameof(L1Brain), () => "这种脏东西，多瞧一眼都嫌烦");
            L2 = this.GetLocalization(nameof(L2), () => "弄干净了就好，别沾一身腥味回来找我");
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

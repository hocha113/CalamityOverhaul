using CalamityOverhaul.Content.Narrative;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Shenyo.Gifts
{
    /// <summary>kikasa.004，蜂后/巨鹿二选一共位，按击杀分支换台词</summary>
    internal sealed class ShenyoQueenBeeOrDeerclopsGift : ShenyoBossGiftNarrative, ILocalizedModType
    {
        public string LocalizationCategory => "ADV.Shenyo";

        public static LocalizedText L0Bee { get; private set; }
        public static LocalizedText L1Bee { get; private set; }
        public static LocalizedText L0Deer { get; private set; }
        public static LocalizedText L1Deer { get; private set; }
        public static LocalizedText L2 { get; private set; }

        public override StyleId DefaultStyle => NarrativeIds.Kikasa;

        public override void SetStaticDefaults() {
            L0Bee = this.GetLocalization(nameof(L0Bee), () => "甜是甜，脾气也冲，倒是配");
            L1Bee = this.GetLocalization(nameof(L1Bee), () => "蜜是好东西，可惜它舍不得给");
            L0Deer = this.GetLocalization(nameof(L0Deer), () => "独眼的，脾气比我还倔");
            L1Deer = this.GetLocalization(nameof(L1Deer), () => "一根木头也能闹出这么大动静");
            L2 = this.GetLocalization(nameof(L2), () => "这一趟，没伤着吧");
        }

        protected override void Build(NarrativeComposer n) {
            bool deer = ShenyoGiftNarrativeTracker.LastDefeatedBossId(GiftId) == NPCID.Deerclops;
            n.Say(NarrativeIds.Shenyo, deer ? L0Deer.Value : L0Bee.Value, deer ? Voice[3] : Voice[1],
                    onEnter: PortraitFace(ShenyoFullBodyPortrait.Face.Wry))
             .Say(NarrativeIds.Shenyo, deer ? L1Deer.Value : L1Bee.Value, deer ? Voice[4] : Voice[2],
                    onEnter: PortraitFace(ShenyoFullBodyPortrait.Face.Scrutiny))
             //台词分支只换文本，节点图仍是单线，两条路径都会经过这一次发放
             .GiftTalisman(GiftId)
             .Say(NarrativeIds.Shenyo, L2.Value, Voice[5], onEnter: PortraitFace(ShenyoFullBodyPortrait.Face.Calm));
        }

        protected override bool IsGiftCompleted() => ShenyoStorySync.GiftStory.QueenBeeOrDeerclopsGift;

        protected override void MarkGiftCompleted() => ShenyoStorySync.GiftStory.QueenBeeOrDeerclopsGift = true;
    }
}

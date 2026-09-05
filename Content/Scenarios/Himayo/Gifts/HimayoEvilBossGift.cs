using CalamityOverhaul.Content.Narrative;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Himayo.Gifts
{
    /// <summary>onikiri.001，B 嫌脏短刺（全线唯一）；世吞/克脑共位，按击杀分支换钉子</summary>
    internal sealed class HimayoEvilBossGift : HimayoBossGiftNarrative, ILocalizedModType
    {
        public string LocalizationCategory => "ADV.Himayo";

        public static LocalizedText L0Worm { get; private set; }
        public static LocalizedText L1Worm { get; private set; }
        public static LocalizedText L0Brain { get; private set; }
        public static LocalizedText L1Brain { get; private set; }

        public override StyleId DefaultStyle => NarrativeIds.Onikiri;

        public override void SetStaticDefaults() {
            L0Worm = this.GetLocalization(nameof(L0Worm), () => "这一截一截挤在烂泥里的家伙，腥气重得像刚翻出来的烂泥沟，可憋死我了");
            L1Worm = this.GetLocalization(nameof(L1Worm), () => "快把刀甩干净收起来，咱们换个通风的地方吹吹风去");
            L0Brain = this.GetLocalization(nameof(L0Brain), () => "湿漉漉一团看着就让人头皮发麻，脑子就该老老实实待在头壳里嘛");
            L1Brain = this.GetLocalization(nameof(L1Brain), () => "刀身沾得黏糊糊的，赶紧拿布擦一擦，可别往你自己衣裳上抹");
        }

        protected override void Build(NarrativeComposer n) {
            bool brain = HimayoGiftNarrativeTracker.LastDefeatedBossId == NPCID.BrainofCthulhu;
            n.Say(NarrativeIds.Mayo, brain ? L0Brain.Value : L0Worm.Value, brain ? Voice[3] : Voice[1],
                    onEnter: PortraitFace(HimayoFullBodyPortrait.Face.Doubt))
             .GiftReward(GiftKey)
             .Say(NarrativeIds.Mayo, brain ? L1Brain.Value : L1Worm.Value, brain ? Voice[4] : Voice[2],
                    onEnter: PortraitFace(HimayoFullBodyPortrait.Face.Ruminate));
        }

        protected override bool IsGiftCompleted()
            => HimayoStorySync.ReadGift(d => d.EvilBossGift, d => d.EvilBossGift);

        protected override void MarkGiftCompleted()
            => HimayoStorySync.WriteGift(d => d.EvilBossGift = true, d => d.EvilBossGift = true);
    }
}

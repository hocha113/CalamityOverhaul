using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.Inscriptions;
using CalamityOverhaul.Content.Narrative;
using InnoVault.Narrative.Audio;
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

        private static NarrativeVoiceBank Voice;

        public override StyleId DefaultStyle => NarrativeIds.Onikiri;

        public override int[] TargetBossIds => [NPCID.EaterofWorldsHead, NPCID.BrainofCthulhu];

        public override void SetStaticDefaults() {
            L0Worm = this.GetLocalization(nameof(L0Worm), () => "恶心……一截一截挤在土里");
            L1Worm = this.GetLocalization(nameof(L1Worm), () => "腥得像刚翻开的泥沟。够了，我不想再闻");
            L0Brain = this.GetLocalization(nameof(L0Brain), () => "脑子就该待在头壳里");
            L1Brain = this.GetLocalization(nameof(L1Brain), () => "湿乎乎摊在外面。我看一眼都嫌");
            Voice = NarrativeVoiceBank.Create(Mod, "Content/Scenarios/Himayo/Lines/Gifts/HimayoEvilBossGift", count: 4);
        }

        protected override void Build(NarrativeComposer n) {
            bool brain = HimayoGiftNarrativeTracker.LastDefeatedBossId == NPCID.BrainofCthulhu;
            n.Say(NarrativeIds.Mayo, brain ? L0Brain.Value : L0Worm.Value, brain ? Voice[3] : Voice[1],
                    onEnter: PortraitFace(HimayoFullBodyPortrait.Face.Doubt))
             .Reward(ModContent.ItemType<OniMeiRubbingChihi>(), title: string.Empty, blocking: false)
             .Say(NarrativeIds.Mayo, brain ? L1Brain.Value : L1Worm.Value, brain ? Voice[4] : Voice[2],
                    onEnter: PortraitFace(HimayoFullBodyPortrait.Face.Ruminate));
        }

        protected override bool IsGiftCompleted()
            => HimayoStorySync.ReadGift(d => d.EvilBossGift, d => d.EvilBossGift);

        protected override void MarkGiftCompleted()
            => HimayoStorySync.WriteGift(d => d.EvilBossGift = true, d => d.EvilBossGift = true);
    }
}

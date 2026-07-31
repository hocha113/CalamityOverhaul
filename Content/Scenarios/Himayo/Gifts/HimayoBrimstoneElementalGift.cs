using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.Inscriptions;
using CalamityOverhaul.Content.Narrative;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Himayo.Gifts
{
    /// <summary>onikiri.006，G 热滩；起-歪-收</summary>
    internal sealed class HimayoBrimstoneElementalGift : HimayoBossGiftNarrative, ILocalizedModType
    {
        public string LocalizationCategory => "ADV.Himayo";

        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }

        public override StyleId DefaultStyle => NarrativeIds.Onikiri;
        public override int TargetBossId => CWRID.NPC_BrimstoneElemental;

        public override void SetStaticDefaults() {
            L0 = this.GetLocalization(nameof(L0), () => "热气还没散。你看那滩，还在冒");
            L1 = this.GetLocalization(nameof(L1), () => "脚伸进去会怎样？……别，我不是让你试");
            L2 = this.GetLocalization(nameof(L2), () => "总之先别碰。烫手");
        }

        protected override void Build(NarrativeComposer n) {
            n.Say(NarrativeIds.Mayo, L0.Value, Voice[1], onEnter: PortraitFace(HimayoFullBodyPortrait.Face.Doubt))
             .Say(NarrativeIds.Mayo, L1.Value, Voice[2], onEnter: PortraitFace(HimayoFullBodyPortrait.Face.Forsmile))
             .Reward(ModContent.ItemType<OniMeiRubbingKogehi>(), title: string.Empty, blocking: false)
             .Say(NarrativeIds.Mayo, L2.Value, Voice[3], onEnter: PortraitFace(HimayoFullBodyPortrait.Face.Doubt));
        }

        protected override bool IsGiftCompleted()
            => HimayoStorySync.ReadGift(d => d.BrimstoneElementalGift, d => d.BrimstoneElementalGift);

        protected override void MarkGiftCompleted()
            => HimayoStorySync.WriteGift(d => d.BrimstoneElementalGift = true, d => d.BrimstoneElementalGift = true);
    }
}

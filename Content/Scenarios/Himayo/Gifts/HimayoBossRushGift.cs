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
    /// <summary>onikiri.021，G 站住收束</summary>
    internal sealed class HimayoBossRushGift : HimayoBossGiftNarrative, ILocalizedModType
    {
        public string LocalizationCategory => "ADV.Himayo";

        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }

        private static NarrativeVoiceBank Voice;

        public override StyleId DefaultStyle => NarrativeIds.Onikiri;
        public override bool IsBossRushGift => true;

        public override void SetStaticDefaults() {
            L0 = this.GetLocalization(nameof(L0), () => "先站住");
            L1 = this.GetLocalization(nameof(L1), () => "别晃。气还没喘匀呢");
            Voice = NarrativeVoiceBank.Create(Mod, "Content/Scenarios/Himayo/Lines/Gifts/HimayoBossRushGift", count: 2);
        }

        protected override void Build(NarrativeComposer n) {
            n.Say(NarrativeIds.Mayo, L0.Value, Voice[1], onEnter: PortraitFace(HimayoFullBodyPortrait.Face.Doubt))
             .Reward(ModContent.ItemType<OniMeiRubbingAshidome>(), title: string.Empty, blocking: false)
             .Say(NarrativeIds.Mayo, L1.Value, Voice[2], onEnter: PortraitFace(HimayoFullBodyPortrait.Face.Ruminate));
        }

        protected override bool IsGiftCompleted()
            => HimayoStorySync.ReadGift(d => d.BossRushGift, d => d.BossRushGift);

        protected override void MarkGiftCompleted()
            => HimayoStorySync.WriteGift(d => d.BossRushGift = true, d => d.BossRushGift = true);
    }
}

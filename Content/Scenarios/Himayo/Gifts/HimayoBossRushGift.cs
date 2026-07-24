using CalamityOverhaul.Content.Narrative;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Himayo.Gifts
{
    /// <summary>onikiri.021，G 站住收束</summary>
    internal sealed class HimayoBossRushGift : HimayoBossGiftNarrative, ILocalizedModType
    {
        public string LocalizationCategory => "ADV.Himayo";

        public static LocalizedText L0 { get; private set; }

        public override StyleId DefaultStyle => NarrativeIds.Onikiri;
        public override bool IsBossRushGift => true;

        public override void SetStaticDefaults() {
            L0 = this.GetLocalization(nameof(L0), () => "先站住。别晃");
        }

        protected override void Build(NarrativeComposer n) {
            n.Say(NarrativeIds.Mayo, L0.Value);
        }

        protected override bool IsGiftCompleted()
            => HimayoStorySync.ReadGift(d => d.BossRushGift, d => d.BossRushGift);

        protected override void MarkGiftCompleted()
            => HimayoStorySync.WriteGift(d => d.BossRushGift = true, d => d.BossRushGift = true);
    }
}

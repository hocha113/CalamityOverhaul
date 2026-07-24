using CalamityOverhaul.Content.Narrative;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Himayo.Gifts
{
    /// <summary>onikiri.006，G 热滩禁令，话少仍在</summary>
    internal sealed class HimayoBrimstoneElementalGift : HimayoBossGiftNarrative, ILocalizedModType
    {
        public string LocalizationCategory => "ADV.Himayo";

        public static LocalizedText L0 { get; private set; }

        public override StyleId DefaultStyle => NarrativeIds.Onikiri;
        public override int TargetBossId => CWRID.NPC_BrimstoneElemental;

        public override void SetStaticDefaults() {
            L0 = this.GetLocalization(nameof(L0), () => "热气还没散。那滩先别碰，烫手");
        }

        protected override void Build(NarrativeComposer n) {
            n.Say(NarrativeIds.Mayo, L0.Value, onEnter: PortraitFace(HimayoFullBodyPortrait.Face.Doubt));
        }

        protected override bool IsGiftCompleted()
            => HimayoStorySync.ReadGift(d => d.BrimstoneElementalGift, d => d.BrimstoneElementalGift);

        protected override void MarkGiftCompleted()
            => HimayoStorySync.WriteGift(d => d.BrimstoneElementalGift = true, d => d.BrimstoneElementalGift = true);
    }
}

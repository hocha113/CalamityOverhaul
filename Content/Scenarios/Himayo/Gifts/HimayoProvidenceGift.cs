using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.Inscriptions;
using CalamityOverhaul.Content.Narrative;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Himayo.Gifts
{
    /// <summary>onikiri.015，E 气烫+塞物</summary>
    internal sealed class HimayoProvidenceGift : HimayoBossGiftNarrative, ILocalizedModType
    {
        public string LocalizationCategory => "ADV.Himayo";

        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }

        public override StyleId DefaultStyle => NarrativeIds.Onikiri;
        public override int TargetBossId => CWRID.NPC_Providence;

        public override void SetStaticDefaults() {
            L0 = this.GetLocalization(nameof(L0), () => "啧，空气还烫");
            L1 = this.GetLocalization(nameof(L1), () => "手背都热得发紧。站这儿像被人拿去烤番薯");
            L2 = this.GetLocalization(nameof(L2), () => "拿着。别在这儿杵着烤了");
        }

        protected override void Build(NarrativeComposer n) {
            n.Say(NarrativeIds.Mayo, L0.Value, Voice[1], onEnter: PortraitFace(HimayoFullBodyPortrait.Face.Doubt))
             .Say(NarrativeIds.Mayo, L1.Value, Voice[2], onEnter: PortraitFace(HimayoFullBodyPortrait.Face.Forsmile))
             .Reward(ModContent.ItemType<OniMeiRubbingKurikara>(), title: string.Empty, blocking: false)
             .Say(NarrativeIds.Mayo, L2.Value, Voice[3]);
        }

        protected override bool IsGiftCompleted()
            => HimayoStorySync.ReadGift(d => d.ProvidenceGift, d => d.ProvidenceGift);

        protected override void MarkGiftCompleted()
            => HimayoStorySync.WriteGift(d => d.ProvidenceGift = true, d => d.ProvidenceGift = true);
    }
}

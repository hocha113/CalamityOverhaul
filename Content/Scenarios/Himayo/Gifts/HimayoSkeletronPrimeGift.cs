using CalamityOverhaul.Content.Narrative;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Himayo.Gifts
{
    /// <summary>onikiri.009，B 头旧身新，幽默违和</summary>
    internal sealed class HimayoSkeletronPrimeGift : HimayoBossGiftNarrative, ILocalizedModType
    {
        public string LocalizationCategory => "ADV.Himayo";

        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }

        public override StyleId DefaultStyle => NarrativeIds.Onikiri;
        public override int TargetBossId => NPCID.SkeletronPrime;

        public override void SetStaticDefaults() {
            L0 = this.GetLocalization(nameof(L0), () => "头还是旧的，身子倒换了新铁");
            L1 = this.GetLocalization(nameof(L1), () => "像换装换到一半就冲出来了。别扭");
        }

        protected override void Build(NarrativeComposer n) {
            n.Say(NarrativeIds.Mayo, L0.Value, onEnter: PortraitFace(HimayoFullBodyPortrait.Face.Doubt))
             .Say(NarrativeIds.Mayo, L1.Value, onEnter: PortraitFace(HimayoFullBodyPortrait.Face.Grin));
        }

        protected override bool IsGiftCompleted()
            => HimayoStorySync.ReadGift(d => d.SkeletronPrimeGift, d => d.SkeletronPrimeGift);

        protected override void MarkGiftCompleted()
            => HimayoStorySync.WriteGift(d => d.SkeletronPrimeGift = true, d => d.SkeletronPrimeGift = true);
    }
}

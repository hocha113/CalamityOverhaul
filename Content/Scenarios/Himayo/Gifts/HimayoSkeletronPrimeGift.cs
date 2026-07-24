using CalamityOverhaul.Content.Narrative;
using InnoVault.Narrative.Audio;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Himayo.Gifts
{
    /// <summary>onikiri.009，B 头旧身新</summary>
    internal sealed class HimayoSkeletronPrimeGift : HimayoBossGiftNarrative, ILocalizedModType
    {
        public string LocalizationCategory => "ADV.Himayo";

        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }

        private static NarrativeVoiceBank Voice;

        public override StyleId DefaultStyle => NarrativeIds.Onikiri;
        public override int TargetBossId => NPCID.SkeletronPrime;

        public override void SetStaticDefaults() {
            L0 = this.GetLocalization(nameof(L0), () => "头还是旧的，身子倒换了新铁");
            L1 = this.GetLocalization(nameof(L1), () => "像换装换到一半就冲出来了，别扭");
            L2 = this.GetLocalization(nameof(L2), () => "下次换完再出来打行不行。看得我都替它着急");
            Voice = NarrativeVoiceBank.Create(Mod, "Content/Scenarios/Himayo/Lines/Gifts/HimayoSkeletronPrimeGift", count: 3);
        }

        protected override void Build(NarrativeComposer n) {
            n.Say(NarrativeIds.Mayo, L0.Value, Voice[1], onEnter: PortraitFace(HimayoFullBodyPortrait.Face.Doubt))
             .Say(NarrativeIds.Mayo, L1.Value, Voice[2], onEnter: PortraitFace(HimayoFullBodyPortrait.Face.Grin))
             .Say(NarrativeIds.Mayo, L2.Value, Voice[3]);
        }

        protected override bool IsGiftCompleted()
            => HimayoStorySync.ReadGift(d => d.SkeletronPrimeGift, d => d.SkeletronPrimeGift);

        protected override void MarkGiftCompleted()
            => HimayoStorySync.WriteGift(d => d.SkeletronPrimeGift = true, d => d.SkeletronPrimeGift = true);
    }
}

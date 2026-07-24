using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.Items;
using CalamityOverhaul.Content.Narrative;
using InnoVault.Narrative.Audio;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Himayo.Gifts
{
    /// <summary>onikiri.014，H 微痕：上面没了 + 站住吗</summary>
    internal sealed class HimayoMoonLordGift : HimayoBossGiftNarrative, ILocalizedModType
    {
        public string LocalizationCategory => "ADV.Himayo";

        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }

        private static NarrativeVoiceBank Voice;

        public override StyleId DefaultStyle => NarrativeIds.Onikiri;
        public override int TargetBossId => NPCID.MoonLordCore;

        public override void SetStaticDefaults() {
            L0 = this.GetLocalization(nameof(L0), () => "上面那摊没了");
            L1 = this.GetLocalization(nameof(L1), () => "说不上轻松……就是空了一块");
            L2 = this.GetLocalization(nameof(L2), () => "你还站得住吗");
            Voice = NarrativeVoiceBank.Create(Mod, "Content/Scenarios/Himayo/Lines/Gifts/HimayoMoonLordGift", count: 3);
        }

        protected override void Build(NarrativeComposer n) {
            n.Say(NarrativeIds.Mayo, L0.Value, Voice[1], onEnter: PortraitFace(HimayoFullBodyPortrait.Face.Doubt))
             .Say(NarrativeIds.Mayo, L1.Value, Voice[2], onEnter: PortraitFace(HimayoFullBodyPortrait.Face.Ruminate))
             .Reward(ModContent.ItemType<OniMeiRubbingShishinoko>(), title: string.Empty, blocking: false)
             .Say(NarrativeIds.Mayo, L2.Value, Voice[3]);
        }

        protected override bool IsGiftCompleted()
            => HimayoStorySync.ReadGift(d => d.MoonLordGift, d => d.MoonLordGift);

        protected override void MarkGiftCompleted()
            => HimayoStorySync.WriteGift(d => d.MoonLordGift = true, d => d.MoonLordGift = true);
    }
}

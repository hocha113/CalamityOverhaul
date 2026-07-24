using CalamityOverhaul.Content.Narrative;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Himayo.Gifts
{
    /// <summary>onikiri.013，G 门口清静</summary>
    internal sealed class HimayoCultistGift : HimayoBossGiftNarrative, ILocalizedModType
    {
        public string LocalizationCategory => "ADV.Himayo";

        public static LocalizedText L0 { get; private set; }

        public override StyleId DefaultStyle => NarrativeIds.Onikiri;
        public override int TargetBossId => NPCID.CultistBoss;

        public override void SetStaticDefaults() {
            L0 = this.GetLocalization(nameof(L0), () => "门口总算清静了。耳朵能歇一歇");
        }

        protected override void Build(NarrativeComposer n) {
            n.Say(NarrativeIds.Mayo, L0.Value, onEnter: PortraitFace(HimayoFullBodyPortrait.Face.Forsmile));
        }

        protected override bool IsGiftCompleted()
            => HimayoStorySync.ReadGift(d => d.CultistGift, d => d.CultistGift);

        protected override void MarkGiftCompleted()
            => HimayoStorySync.WriteGift(d => d.CultistGift = true, d => d.CultistGift = true);
    }
}

using CalamityOverhaul.Content.Narrative;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Himayo.Gifts
{
    /// <summary>onikiri.017，B 空/大，略紧非洁癖</summary>
    internal sealed class HimayoDevourerOfGodsGift : HimayoBossGiftNarrative, ILocalizedModType
    {
        public string LocalizationCategory => "ADV.Himayo";

        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }

        public override StyleId DefaultStyle => NarrativeIds.Onikiri;
        public override int TargetBossId => CWRID.NPC_DevourerofGodsHead;

        public override void SetStaticDefaults() {
            L0 = this.GetLocalization(nameof(L0), () => "太大了。空得像把天挖掉了一块");
            L1 = this.GetLocalization(nameof(L1), () => "别看太久。看久了，心里也空");
        }

        protected override void Build(NarrativeComposer n) {
            n.Say(NarrativeIds.Mayo, L0.Value, onEnter: PortraitFace(HimayoFullBodyPortrait.Face.Doubt))
             .Say(NarrativeIds.Mayo, L1.Value);
        }

        protected override bool IsGiftCompleted()
            => HimayoStorySync.ReadGift(d => d.DevourerOfGodsGift, d => d.DevourerOfGodsGift);

        protected override void MarkGiftCompleted()
            => HimayoStorySync.WriteGift(d => d.DevourerOfGodsGift = true, d => d.DevourerOfGodsGift = true);
    }
}

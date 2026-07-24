using CalamityOverhaul.Content.Narrative;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Himayo.Gifts
{
    /// <summary>onikiri.018，F 刃烫+搁一边</summary>
    internal sealed class HimayoYharonGift : HimayoBossGiftNarrative, ILocalizedModType
    {
        public string LocalizationCategory => "ADV.Himayo";

        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }

        public override StyleId DefaultStyle => NarrativeIds.Onikiri;
        public override int TargetBossId => CWRID.NPC_Yharon;

        public override void SetStaticDefaults() {
            L0 = this.GetLocalization(nameof(L0), () => "刃还烫。我这边也热得慌");
            L1 = this.GetLocalization(nameof(L1), () => "先别急着入鞘。烫着难受的是我");
            L2 = this.GetLocalization(nameof(L2), () => "这个，先搁一边。回头再说");
        }

        protected override void Build(NarrativeComposer n) {
            n.Say(NarrativeIds.Mayo, L0.Value, onEnter: PortraitFace(HimayoFullBodyPortrait.Face.Doubt))
             .Say(NarrativeIds.Mayo, L1.Value)
             .SayReward(NarrativeIds.Mayo, L2.Value, ItemID.IronPickaxe, title: string.Empty);
        }

        protected override bool IsGiftCompleted()
            => HimayoStorySync.ReadGift(d => d.YharonGift, d => d.YharonGift);

        protected override void MarkGiftCompleted()
            => HimayoStorySync.WriteGift(d => d.YharonGift = true, d => d.YharonGift = true);
    }
}

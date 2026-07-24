using CalamityOverhaul.Content.Narrative;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Himayo.Gifts
{
    /// <summary>onikiri.012，C 叮人+护刀半句</summary>
    internal sealed class HimayoGolemGift : HimayoBossGiftNarrative, ILocalizedModType
    {
        public string LocalizationCategory => "ADV.Himayo";

        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }

        public override StyleId DefaultStyle => NarrativeIds.Onikiri;
        public override int TargetBossId => NPCID.Golem;

        public override void SetStaticDefaults() {
            L0 = this.GetLocalization(nameof(L0), () => "手腕还麻吗。肩膀也缓缓");
            L1 = this.GetLocalization(nameof(L1), () => "石头硬，硬碰硬最伤人");
            L2 = this.GetLocalization(nameof(L2), () => "刀也别拿去硬磕。刃崩了，我住里面也不舒服");
        }

        protected override void Build(NarrativeComposer n) {
            n.Say(NarrativeIds.Mayo, L0.Value, onEnter: PortraitFace(HimayoFullBodyPortrait.Face.Ruminate))
             .Say(NarrativeIds.Mayo, L1.Value)
             .Say(NarrativeIds.Mayo, L2.Value);
        }

        protected override bool IsGiftCompleted()
            => HimayoStorySync.ReadGift(d => d.GolemGift, d => d.GolemGift);

        protected override void MarkGiftCompleted()
            => HimayoStorySync.WriteGift(d => d.GolemGift = true, d => d.GolemGift = true);
    }
}

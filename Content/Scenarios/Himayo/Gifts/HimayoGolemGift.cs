using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.Inscriptions;
using CalamityOverhaul.Content.Narrative;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Himayo.Gifts
{
    /// <summary>onikiri.012，C 叮人+护刀</summary>
    internal sealed class HimayoGolemGift : HimayoBossGiftNarrative, ILocalizedModType
    {
        public string LocalizationCategory => "ADV.Himayo";

        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }

        public override StyleId DefaultStyle => NarrativeIds.Onikiri;
        public override int TargetBossId => NPCID.Golem;

        public override void SetStaticDefaults() {
            L0 = this.GetLocalization(nameof(L0), () => "手腕还麻吗？肩膀也缓缓");
            L1 = this.GetLocalization(nameof(L1), () => "石头这东西，撞一下，麻意能窜到手心");
            L2 = this.GetLocalization(nameof(L2), () => "我以前也被震得虎口发麻，可不舒服");
            L3 = this.GetLocalization(nameof(L3), () => "主要刀也别拿去硬磕。刃崩了，我会不舒服");
        }

        protected override void Build(NarrativeComposer n) {
            n.Say(NarrativeIds.Mayo, L0.Value, Voice[1], onEnter: PortraitFace(HimayoFullBodyPortrait.Face.Ruminate))
             .Say(NarrativeIds.Mayo, L1.Value, Voice[2])
             .Say(NarrativeIds.Mayo, L2.Value, Voice[3])
             .Reward(ModContent.ItemType<OniMeiRubbingShibori>(), title: string.Empty, blocking: false)
             .Say(NarrativeIds.Mayo, L3.Value, Voice[4], onEnter: PortraitFace(HimayoFullBodyPortrait.Face.Forsmile));
        }

        protected override bool IsGiftCompleted()
            => HimayoStorySync.ReadGift(d => d.GolemGift, d => d.GolemGift);

        protected override void MarkGiftCompleted()
            => HimayoStorySync.WriteGift(d => d.GolemGift = true, d => d.GolemGift = true);
    }
}

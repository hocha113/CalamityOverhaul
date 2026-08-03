using CalamityOverhaul.Content.Narrative;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Himayo.Gifts
{
    /// <summary>onikiri.020，C 空气沉+清醒；接受一笔</summary>
    internal sealed class HimayoSupremeCalamitasGift : HimayoBossGiftNarrative, ILocalizedModType
    {
        public string LocalizationCategory => "ADV.Himayo";

        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }

        public override StyleId DefaultStyle => NarrativeIds.Onikiri;
        public override int TargetBossId => CWRID.NPC_SupremeCalamitas;

        public override void SetStaticDefaults() {
            L0 = this.GetLocalization(nameof(L0), () => "那边弄得空气发沉。发滞那种");
            L1 = this.GetLocalization(nameof(L1), () => "去吹吹风，或者洗把脸。不是嫌脏，是让你清醒点");
            L2 = this.GetLocalization(nameof(L2), () => "这种程度……我倒习惯了。你别愣着就行");
        }

        protected override void Build(NarrativeComposer n) {
            n.Say(NarrativeIds.Mayo, L0.Value, Voice[1], onEnter: PortraitFace(HimayoFullBodyPortrait.Face.Ruminate))
             .Say(NarrativeIds.Mayo, L1.Value, Voice[2])
             .GiftReward(GiftKey)
             .Say(NarrativeIds.Mayo, L2.Value, Voice[3], onEnter: PortraitFace(HimayoFullBodyPortrait.Face.Forsmile));
        }

        protected override bool IsGiftCompleted()
            => HimayoStorySync.ReadGift(d => d.SupremeCalamitasGift, d => d.SupremeCalamitasGift);

        protected override void MarkGiftCompleted()
            => HimayoStorySync.WriteGift(d => d.SupremeCalamitasGift = true, d => d.SupremeCalamitasGift = true);
    }
}

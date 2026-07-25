using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.Inscriptions;
using CalamityOverhaul.Content.Narrative;
using InnoVault.Narrative.Audio;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Himayo.Gifts
{
    /// <summary>onikiri.019，A 耳嗡，碎嘴关心</summary>
    internal sealed class HimayoExoMechsGift : HimayoBossGiftNarrative, ILocalizedModType
    {
        public string LocalizationCategory => "ADV.Himayo";

        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }

        private static NarrativeVoiceBank Voice;

        public override StyleId DefaultStyle => NarrativeIds.Onikiri;

        public override int[] TargetBossIds => [
            CWRID.NPC_AresBody,
            CWRID.NPC_Apollo,
            CWRID.NPC_Artemis,
            CWRID.NPC_ThanatosHead,
        ];

        public override void SetStaticDefaults() {
            L0 = this.GetLocalization(nameof(L0), () => "轰完了，耳朵里还在嗡");
            L1 = this.GetLocalization(nameof(L1), () => "像有人拿小锤子在里头敲。烦人");
            L2 = this.GetLocalization(nameof(L2), () => "要不要先避一下吵的地方。让耳朵歇歇");
            Voice = NarrativeVoiceBank.Create(Mod, "Content/Scenarios/Himayo/Lines/Gifts/HimayoExoMechsGift", count: 3);
        }

        protected override void Build(NarrativeComposer n) {
            n.Say(NarrativeIds.Mayo, L0.Value, Voice[1], onEnter: PortraitFace(HimayoFullBodyPortrait.Face.Ruminate))
             .Say(NarrativeIds.Mayo, L1.Value, Voice[2], onEnter: PortraitFace(HimayoFullBodyPortrait.Face.Doubt))
             .Reward(ModContent.ItemType<OniMeiRubbingChinmei>(), title: string.Empty, blocking: false)
             .Say(NarrativeIds.Mayo, L2.Value, Voice[3], onEnter: PortraitFace(HimayoFullBodyPortrait.Face.Forsmile));
        }

        protected override bool IsGiftCompleted()
            => HimayoStorySync.ReadGift(d => d.ExoMechsGift, d => d.ExoMechsGift);

        protected override void MarkGiftCompleted()
            => HimayoStorySync.WriteGift(d => d.ExoMechsGift = true, d => d.ExoMechsGift = true);
    }
}

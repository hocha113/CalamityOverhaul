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
    /// <summary>onikiri.016，D 潮的生活联想，非霉洁癖</summary>
    internal sealed class HimayoPolterghastGift : HimayoBossGiftNarrative, ILocalizedModType
    {
        public string LocalizationCategory => "ADV.Himayo";

        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }

        private static NarrativeVoiceBank Voice;

        public override StyleId DefaultStyle => NarrativeIds.Onikiri;
        public override int TargetBossId => CWRID.NPC_Polterghast;

        public override void SetStaticDefaults() {
            L0 = this.GetLocalization(nameof(L0), () => "潮得像屋里晾了一个礼拜的衣服，怎么都干不了");
            L1 = this.GetLocalization(nameof(L1), () => "这种地方睡觉，醒来腰一定疼");
            L2 = this.GetLocalization(nameof(L2), () => "开窗？这儿哪来的窗。炭也没有");
            L3 = this.GetLocalization(nameof(L3), () => "反正别在这儿过夜。听劝");
            Voice = NarrativeVoiceBank.Create(Mod, "Content/Scenarios/Himayo/Lines/Gifts/HimayoPolterghastGift", count: 4);
        }

        protected override void Build(NarrativeComposer n) {
            n.Say(NarrativeIds.Mayo, L0.Value, Voice[1], onEnter: PortraitFace(HimayoFullBodyPortrait.Face.Ruminate))
             .Say(NarrativeIds.Mayo, L1.Value, Voice[2])
             .Say(NarrativeIds.Mayo, L2.Value, Voice[3], onEnter: PortraitFace(HimayoFullBodyPortrait.Face.Forsmile))
             .Reward(ModContent.ItemType<OniMeiRubbingShiohi>(), title: string.Empty, blocking: false)
             .Say(NarrativeIds.Mayo, L3.Value, Voice[4], onEnter: PortraitFace(HimayoFullBodyPortrait.Face.Ruminate));
        }

        protected override bool IsGiftCompleted()
            => HimayoStorySync.ReadGift(d => d.PolterghastGift, d => d.PolterghastGift);

        protected override void MarkGiftCompleted()
            => HimayoStorySync.WriteGift(d => d.PolterghastGift = true, d => d.PolterghastGift = true);
    }
}

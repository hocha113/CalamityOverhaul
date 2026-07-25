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
    /// <summary>onikiri.004，H 关系微痕；天气式提醒，不问安</summary>
    internal sealed class HimayoWallOfFleshGift : HimayoBossGiftNarrative, ILocalizedModType
    {
        public string LocalizationCategory => "ADV.Himayo";

        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }

        private static NarrativeVoiceBank Voice;

        public override StyleId DefaultStyle => NarrativeIds.Onikiri;
        public override int TargetBossId => NPCID.WallofFlesh;

        public override void SetStaticDefaults() {
            L0 = this.GetLocalization(nameof(L0), () => "……真倒了。墙倒了");
            L1 = this.GetLocalization(nameof(L1), () => "说不上高兴不高兴。就是感觉……后面要变天了");
            L2 = this.GetLocalization(nameof(L2), () => "……你自己心里有数就行");
            Voice = NarrativeVoiceBank.Create(Mod, "Content/Scenarios/Himayo/Lines/Gifts/HimayoWallOfFleshGift", count: 3);
        }

        protected override void Build(NarrativeComposer n) {
            n.Say(NarrativeIds.Mayo, L0.Value, Voice[1], onEnter: PortraitFace(HimayoFullBodyPortrait.Face.Doubt))
             .Say(NarrativeIds.Mayo, L1.Value, Voice[2], onEnter: PortraitFace(HimayoFullBodyPortrait.Face.Ruminate))
             .Reward(ModContent.ItemType<OniMeiRubbingTomokiri>(), title: string.Empty, blocking: false)
             .Say(NarrativeIds.Mayo, L2.Value, Voice[3]);
        }

        protected override bool IsGiftCompleted()
            => HimayoStorySync.ReadGift(d => d.WallOfFleshGift, d => d.WallOfFleshGift);

        protected override void MarkGiftCompleted()
            => HimayoStorySync.WriteGift(d => d.WallOfFleshGift = true, d => d.WallOfFleshGift = true);
    }
}

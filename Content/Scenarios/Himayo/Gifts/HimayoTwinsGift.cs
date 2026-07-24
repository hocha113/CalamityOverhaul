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
    /// <summary>onikiri.008，D 跑题对视笑场</summary>
    internal sealed class HimayoTwinsGift : HimayoBossGiftNarrative, ILocalizedModType
    {
        public string LocalizationCategory => "ADV.Himayo";

        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }

        private static NarrativeVoiceBank Voice;

        public override StyleId DefaultStyle => NarrativeIds.Onikiri;

        public override int[] TargetBossIds => [NPCID.Retinazer, NPCID.Spazmatism];

        public override void SetStaticDefaults() {
            L0 = this.GetLocalization(nameof(L0), () => "两个人对视太久，会忍不住笑");
            L1 = this.GetLocalization(nameof(L1), () => "你懂吧。越忍越想笑，嘴角自己就翘起来那种");
            L2 = this.GetLocalization(nameof(L2), () => "我以前跟人比赛谁先憋不住。每次都是我输");
            L3 = this.GetLocalization(nameof(L3), () => "看着那两只……算了，我又想笑了");
            Voice = NarrativeVoiceBank.Create(Mod, "Content/Scenarios/Himayo/Lines/Gifts/HimayoTwinsGift", count: 4);
        }

        protected override void Build(NarrativeComposer n) {
            n.Say(NarrativeIds.Mayo, L0.Value, Voice[1], onEnter: PortraitFace(HimayoFullBodyPortrait.Face.Grin))
             .Say(NarrativeIds.Mayo, L1.Value, Voice[2])
             .Say(NarrativeIds.Mayo, L2.Value, Voice[3], onEnter: PortraitFace(HimayoFullBodyPortrait.Face.Forsmile))
             .Reward(ModContent.ItemType<OniMeiRubbingIkiai>(), title: string.Empty, blocking: false)
             .Say(NarrativeIds.Mayo, L3.Value, Voice[4], onEnter: PortraitFace(HimayoFullBodyPortrait.Face.Grin));
        }

        protected override bool IsGiftCompleted()
            => HimayoStorySync.ReadGift(d => d.TwinsGift, d => d.TwinsGift);

        protected override void MarkGiftCompleted()
            => HimayoStorySync.WriteGift(d => d.TwinsGift = true, d => d.TwinsGift = true);
    }
}

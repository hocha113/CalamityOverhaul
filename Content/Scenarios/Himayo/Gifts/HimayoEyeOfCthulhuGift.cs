using CalamityOverhaul.Content.Narrative;
using InnoVault.Narrative.Audio;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Himayo.Gifts
{
    /// <summary>onikiri.000，D 跑题睡相小故事，无递物</summary>
    internal sealed class HimayoEyeOfCthulhuGift : HimayoBossGiftNarrative, ILocalizedModType
    {
        public string LocalizationCategory => "ADV.Himayo";

        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }
        public static LocalizedText L4 { get; private set; }

        private static NarrativeVoiceBank Voice;

        public override StyleId DefaultStyle => NarrativeIds.Onikiri;
        public override int TargetBossId => NPCID.EyeofCthulhu;

        public override void SetStaticDefaults() {
            L0 = this.GetLocalization(nameof(L0), () => "对了，我们那儿以前有个人，睡觉的时候眼睛合不上");
            L1 = this.GetLocalization(nameof(L1), () => "不是吓人那种睁大，是眼皮半耷着，正对着房梁，像在发呆");
            L2 = this.GetLocalization(nameof(L2), () => "被子裹得严严实实，屋里静得要命。有一回我路过窗下，差点喊人");
            L3 = this.GetLocalization(nameof(L3), () => "结果人家第二天还好好的，在田里喊我帮忙搬花盆");
            L4 = this.GetLocalization(nameof(L4), () => "哎，说着说着，看着你那双眼……我怎么也困了。不说了");
            Voice = NarrativeVoiceBank.Create(Mod, "Content/Scenarios/Himayo/Lines/Gifts/HimayoEyeOfCthulhuGift", count: 5);
        }

        protected override void Build(NarrativeComposer n) {
            n.Say(NarrativeIds.Mayo, L0.Value, Voice[1], onEnter: PortraitFace(HimayoFullBodyPortrait.Face.Ruminate))
             .Say(NarrativeIds.Mayo, L1.Value, Voice[2])
             .Say(NarrativeIds.Mayo, L2.Value, Voice[3])
             .Say(NarrativeIds.Mayo, L3.Value, Voice[4], onEnter: PortraitFace(HimayoFullBodyPortrait.Face.Forsmile))
             .Say(NarrativeIds.Mayo, L4.Value, Voice[5]);
        }

        protected override bool IsGiftCompleted()
            => HimayoStorySync.ReadGift(d => d.EyeOfCthulhuGift, d => d.EyeOfCthulhuGift);

        protected override void MarkGiftCompleted()
            => HimayoStorySync.WriteGift(d => d.EyeOfCthulhuGift = true, d => d.EyeOfCthulhuGift = true);
    }
}

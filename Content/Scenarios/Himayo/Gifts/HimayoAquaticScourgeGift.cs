using CalamityOverhaul.Content.Narrative;
using InnoVault.Narrative.Audio;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Himayo.Gifts
{
    /// <summary>onikiri.005，C 护刀+幽默；海鲜铺笑话</summary>
    internal sealed class HimayoAquaticScourgeGift : HimayoBossGiftNarrative, ILocalizedModType
    {
        public string LocalizationCategory => "ADV.Himayo";

        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }

        private static NarrativeVoiceBank Voice;

        public override StyleId DefaultStyle => NarrativeIds.Onikiri;
        public override int TargetBossId => CWRID.NPC_AquaticScourgeHead;

        public override void SetStaticDefaults() {
            L0 = this.GetLocalization(nameof(L0), () => "等等。刃上这一股……");
            L1 = this.GetLocalization(nameof(L1), () => "我住里面啊。现在跟开了家海鲜铺差不多");
            L2 = this.GetLocalization(nameof(L2), () => "还没法开窗。刀哪里有窗");
            L3 = this.GetLocalization(nameof(L3), () => "你倒是痛快，整柄往味里杵。下次轻着点，行不行");
            Voice = NarrativeVoiceBank.Create(Mod, "Content/Scenarios/Himayo/Lines/Gifts/HimayoAquaticScourgeGift", count: 4);
        }

        protected override void Build(NarrativeComposer n) {
            n.Say(NarrativeIds.Mayo, L0.Value, Voice[1], onEnter: PortraitFace(HimayoFullBodyPortrait.Face.Doubt))
             .Say(NarrativeIds.Mayo, L1.Value, Voice[2], onEnter: PortraitFace(HimayoFullBodyPortrait.Face.Forsmile))
             .Say(NarrativeIds.Mayo, L2.Value, Voice[3])
             .Say(NarrativeIds.Mayo, L3.Value, Voice[4]);
        }

        protected override bool IsGiftCompleted()
            => HimayoStorySync.ReadGift(d => d.AquaticScourgeGift, d => d.AquaticScourgeGift);

        protected override void MarkGiftCompleted()
            => HimayoStorySync.WriteGift(d => d.AquaticScourgeGift = true, d => d.AquaticScourgeGift = true);
    }
}

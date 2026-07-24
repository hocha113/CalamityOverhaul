using CalamityOverhaul.Content.Narrative;
using InnoVault.Narrative.Audio;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Himayo.Gifts
{
    /// <summary>onikiri.011，A 剪败花，卖花口吻</summary>
    internal sealed class HimayoPlanteraGift : HimayoBossGiftNarrative, ILocalizedModType
    {
        public string LocalizationCategory => "ADV.Himayo";

        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }

        private static NarrativeVoiceBank Voice;

        public override StyleId DefaultStyle => NarrativeIds.Onikiri;
        public override int TargetBossId => NPCID.Plantera;

        public override void SetStaticDefaults() {
            L0 = this.GetLocalization(nameof(L0), () => "花开败了，就该剪掉");
            L1 = this.GetLocalization(nameof(L1), () => "梗留着也没意思。占地方，还看着人心烦");
            L2 = this.GetLocalization(nameof(L2), () => "我们那儿卖花的都这么干。败了就剪，别磨蹭");
            L3 = this.GetLocalization(nameof(L3), () => "哎，说这个干嘛。你又不是来买花的");
            Voice = NarrativeVoiceBank.Create(Mod, "Content/Scenarios/Himayo/Lines/Gifts/HimayoPlanteraGift", count: 4);
        }

        protected override void Build(NarrativeComposer n) {
            n.Say(NarrativeIds.Mayo, L0.Value, Voice[1], onEnter: PortraitFace(HimayoFullBodyPortrait.Face.Ruminate))
             .Say(NarrativeIds.Mayo, L1.Value, Voice[2])
             .Say(NarrativeIds.Mayo, L2.Value, Voice[3], onEnter: PortraitFace(HimayoFullBodyPortrait.Face.Forsmile))
             .Reward(ItemID.IronPickaxe, title: string.Empty, blocking: false)
             .Say(NarrativeIds.Mayo, L3.Value, Voice[4], onEnter: PortraitFace(HimayoFullBodyPortrait.Face.Grin));
        }

        protected override bool IsGiftCompleted()
            => HimayoStorySync.ReadGift(d => d.PlanteraGift, d => d.PlanteraGift);

        protected override void MarkGiftCompleted()
            => HimayoStorySync.WriteGift(d => d.PlanteraGift = true, d => d.PlanteraGift = true);
    }
}

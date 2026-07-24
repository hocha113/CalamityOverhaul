using CalamityOverhaul.Content.Narrative;
using InnoVault.Narrative.Audio;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Himayo.Gifts
{
    /// <summary>onikiri.007，F 贫名字+弱提</summary>
    internal sealed class HimayoDestroyerGift : HimayoBossGiftNarrative, ILocalizedModType
    {
        public string LocalizationCategory => "ADV.Himayo";

        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }

        private static NarrativeVoiceBank Voice;

        public override StyleId DefaultStyle => NarrativeIds.Onikiri;
        public override int TargetBossId => NPCID.TheDestroyer;

        public override void SetStaticDefaults() {
            L0 = this.GetLocalization(nameof(L0), () => "叫毁灭者……听着挺唬人的");
            L1 = this.GetLocalization(nameof(L1), () => "结果不就是很长的一截铁吗");
            L2 = this.GetLocalization(nameof(L2), () => "那个，别掉地上。掉了我可不管捡");
            Voice = NarrativeVoiceBank.Create(Mod, "Content/Scenarios/Himayo/Lines/Gifts/HimayoDestroyerGift", count: 3);
        }

        protected override void Build(NarrativeComposer n) {
            n.Say(NarrativeIds.Mayo, L0.Value, Voice[1], onEnter: PortraitFace(HimayoFullBodyPortrait.Face.Grin))
             .Say(NarrativeIds.Mayo, L1.Value, Voice[2])
             .Reward(ItemID.IronPickaxe, title: string.Empty, blocking: false)
             .Say(NarrativeIds.Mayo, L2.Value, Voice[3]);
        }

        protected override bool IsGiftCompleted()
            => HimayoStorySync.ReadGift(d => d.DestroyerGift, d => d.DestroyerGift);

        protected override void MarkGiftCompleted()
            => HimayoStorySync.WriteGift(d => d.DestroyerGift = true, d => d.DestroyerGift = true);
    }
}

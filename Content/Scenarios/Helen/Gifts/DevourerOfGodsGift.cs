using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Helen.Gifts
{
    internal sealed class DevourerOfGodsGift : HelenBossGiftNarrative, ILocalizedModType
    {
        public string LocalizationCategory => "ADV";

        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }
        public static LocalizedText L4 { get; private set; }

        public override StyleId DefaultStyle => "Sea";

        public override int TargetBossId => CWRID.NPC_DevourerofGodsHead;

        public override void SetStaticDefaults() {
            L0 = this.GetLocalization(nameof(L0), () => "神明吞噬者……一条以神为食的宇宙之蛇。它的胃口和它的野心一样无限");
            L1 = this.GetLocalization(nameof(L1), () => "你知道吗？当你凝视深渊时，深渊也在凝视你。但这家伙不止凝视，它还想把你当零食");
            L2 = this.GetLocalization(nameof(L2), () => "霓虹四脚鱼，从虚空裂隙里飘出来的。它发出的光不属于这个维度");
            L3 = this.GetLocalization(nameof(L3), () => "看着它会让你的大脑尝试理解不该理解的颜色。这是一种……独特的体验");
            L4 = this.GetLocalization(nameof(L4), () => "如果你开始看到新的颜色，恭喜。如果你开始闻到颜色，那就该休息了");
        }

        protected override void Build(NarrativeComposer n) {
            n
             .Say("Helen", L0.Value)
             .Say("Helen", "Enjoy", L1.Value)
             .SayReward("Helen", L2.Value, ItemID.NeonTetra, title: string.Empty)
             .Say("Helen", L3.Value)
             .Say("Helen", L4.Value);
        }

        protected override bool IsGiftCompleted()
            => HalibutStorySync.ReadGift(d => d.DevourerOfGodsGift, d => d.DevourerOfGodsGift);

        protected override void MarkGiftCompleted()
            => HalibutStorySync.WriteGift(d => d.DevourerOfGodsGift = true, d => d.DevourerOfGodsGift = true);
    }
}

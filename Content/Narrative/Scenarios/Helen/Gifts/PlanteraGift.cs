using CalamityOverhaul.Content.Narrative.Scenarios.Helen;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Narrative.Scenarios.Helen.Gifts
{
    internal sealed class PlanteraGift : HelenBossGiftNarrative, ILocalizedModType
    {
        public string LocalizationCategory => "ADV";

        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }
        public static LocalizedText L4 { get; private set; }
        public static LocalizedText R1 { get; private set; }

        public override StyleId DefaultStyle => "Sea";

        public override int TargetBossId => NPCID.Plantera;

        public override void SetStaticDefaults() {
            R1 = this.GetLocalization(nameof(R1), () => "比目鱼");
            L0 = this.GetLocalization(nameof(L0), () => "一朵花……如果'花'这个词可以用来形容那种会吃人的藤蔓");
            L1 = this.GetLocalization(nameof(L1), () => "丛林的愤怒以植物的形式生长。大自然的报复从来不讲道理");
            L2 = this.GetLocalization(nameof(L2), () => "双鳕鱼，它有两个头。丛林就像是陆地上的海洋，它们的东西都不需要理由就能长出多余的部位");
            L3 = this.GetLocalization(nameof(L3), () => "据说两个头意味着双倍的智慧，但看起来它们只是在互相争论该往哪游");
            L4 = this.GetLocalization(nameof(L4), () => "就像减肥和食欲，永远在吵架，不过我的食欲总是赢");
        }

        protected override void Build(NarrativeComposer n) {
            n
             .Say("Helen", L0.Value)
             .Say("Helen", L1.Value)
             .SayReward("Helen", L2.Value, ItemID.DoubleCod, title: string.Empty)
             .Say("Helen", L3.Value)
             .Say("Helen", "Naughty2", L4.Value);
        }

        protected override bool IsGiftCompleted()
            => HalibutStorySync.ReadGift(d => d.PlanteraGift, d => d.PlanteraGift);

        protected override void MarkGiftCompleted()
            => HalibutStorySync.WriteGift(d => d.PlanteraGift = true, d => d.PlanteraGift = true);
    }
}

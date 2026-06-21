using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Grip;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Shepel.Gifts
{
    internal sealed class ShepelSlimeGodGift : ShepelBossGiftNarrative, ILocalizedModType
    {
        public string LocalizationCategory => "ADV.Shepel";
        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }

        public override StyleId DefaultStyle => "SHPC";

        public override int TargetBossId => CWRID.NPC_SlimeGodCore;

        public override void SetStaticDefaults() {
            L0 = this.GetLocalization(nameof(L0), () => "居然能分裂成这么多块……这场战斗弄得满地都是黏糊糊的凝胶。");
            L1 = this.GetLocalization(nameof(L1), () => "不过，我发现这些史莱姆的内部结构非常有弹性。我借用了一点，给您的武器握把加了一层柔性缓冲。");
            L2 = this.GetLocalization(nameof(L2), () => "您再试着举起武器看看？重心的分布应该变得更舒服了。");
            L3 = this.GetLocalization(nameof(L3), () => "哪怕战况再怎么混乱，只要手感依旧稳固，您就能永远保持从容。");
        }

        protected override void Build(NarrativeComposer n) {
            n
             .Say("SHPC", L0.Value)
             .Say("SHPC", L1.Value)
             .SayReward("SHPC", L2.Value, ModContent.ItemType<BalancedGripModule>(), title: string.Empty)
             .Say("SHPC", L3.Value, onEnter: PortraitHappy);
        }

        protected override bool IsGiftCompleted()
            => ShepelStorySync.ReadGift(d => d.SlimeGodGift, d => d.SlimeGodGift);

        protected override void MarkGiftCompleted()
            => ShepelStorySync.WriteGift(d => d.SlimeGodGift = true, d => d.SlimeGodGift = true);
    }
}

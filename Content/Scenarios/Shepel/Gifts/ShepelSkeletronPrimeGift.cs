using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Stock;
using CalamityOverhaul.Content.Scenarios.Shepel;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Shepel.Gifts
{
    internal sealed class ShepelSkeletronPrimeGift : ShepelBossGiftNarrative, ILocalizedModType
    {
        public string LocalizationCategory => "ADV.Shepel";

        public static LocalizedText RoleName { get; private set; }
        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }

        public override StyleId DefaultStyle => "SHPC";

        public override int TargetBossId => NPCID.SkeletronPrime;

        public override void SetStaticDefaults() {
RoleName = this.GetLocalization(nameof(RoleName), () => "SHPC");
            L0 = this.GetLocalization(nameof(L0), () => "挥舞着四条手臂的重型骨架……攻击频率确实很快，但机动逻辑过于死板。");
            L1 = this.GetLocalization(nameof(L1), () => "它只是在机械地重复行为，根本无法适应您的战术变化。");
            L2 = this.GetLocalization(nameof(L2), () => "我提炼了它在切换多种武器时的优势，为您优化了武器的突击响应速度。");
            L3 = this.GetLocalization(nameof(L3), () => "缺乏学习能力的旧式机械，只能沦为我的数据养料。");
        }

        protected override void Build(NarrativeComposer n) {
            n
             .Say("SHPC", L0.Value)
             .Say("SHPC", L1.Value)
             .SayReward("SHPC", L2.Value, ModContent.ItemType<AssaultStockModule>(), title: string.Empty)
             .Say("SHPC", L3.Value, onEnter: PortraitSmirk);
        }

        protected override bool IsGiftCompleted()
            => ShepelStorySync.ReadGift(d => d.SkeletronPrimeGift, d => d.SkeletronPrimeGift);

        protected override void MarkGiftCompleted()
            => ShepelStorySync.WriteGift(d => d.SkeletronPrimeGift = true, d => d.SkeletronPrimeGift = true);
    }
}

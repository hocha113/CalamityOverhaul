using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Power;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Shepel.Gifts
{
    internal sealed class ShepelProvidenceGift : ShepelBossGiftNarrative, ILocalizedModType
    {
        public string LocalizationCategory => "ADV.Shepel";

        public static LocalizedText RoleName { get; private set; }
        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }

        public override StyleId DefaultStyle => "SHPC";

        public override int TargetBossId => CWRID.NPC_Providence;

        public override void SetStaticDefaults() {
            RoleName = this.GetLocalization(nameof(RoleName), () => "SHPC");
            L0 = this.GetLocalization(nameof(L0), () => "天神倒下时释放的热量真是恐怖。");
            L1 = this.GetLocalization(nameof(L1), () => "主人，请稍微退后几步，剩下的能量收尾和清扫工作请交给我。");
            L2 = this.GetLocalization(nameof(L2), () => "我将那股四溢的圣火压缩成了这枚小巧的蓄能核心，它能大幅提升您的能量周转率。");
            L3 = this.GetLocalization(nameof(L3), () => "充能效率非常棒，不过使用时请当心烫手。");
        }

        protected override void Build(NarrativeComposer n) {
            n
             .Say("SHPC", L0.Value)
             .Say("SHPC", L1.Value)
             .SayReward("SHPC", L2.Value, ModContent.ItemType<OverloadCoreModule>(), title: string.Empty)
             .Say("SHPC", L3.Value);
        }

        protected override bool IsGiftCompleted()
            => ShepelStorySync.ReadGift(d => d.ProvidenceGift, d => d.ProvidenceGift);

        protected override void MarkGiftCompleted()
            => ShepelStorySync.WriteGift(d => d.ProvidenceGift = true, d => d.ProvidenceGift = true);
    }
}

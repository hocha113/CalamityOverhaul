using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Barrel;
using CalamityOverhaul.Content.Narrative.Scenarios.Shepel;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Narrative.Scenarios.Shepel.Gifts
{
    internal sealed class ShepelYharonGift : ShepelBossGiftNarrative, ILocalizedModType
    {
        public string LocalizationCategory => "ADV.Shepel";

        public static LocalizedText RoleName { get; private set; }
        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }

        public override StyleId DefaultStyle => "SHPC";

        public override int TargetBossId => CWRID.NPC_Yharon;

        public override void SetStaticDefaults() {
RoleName = this.GetLocalization(nameof(RoleName), () => "SHPC");
            L0 = this.GetLocalization(nameof(L0), () => "那条龙的飞行速度……已经突破了我的追踪极限。");
            L1 = this.GetLocalization(nameof(L1), () => "为了不让您暴露在它的高速扑击下，我在实战中重写了底层的预测模块。");
            L2 = this.GetLocalization(nameof(L2), () => "现在，您的武器初速和响应速度已经得到了显著提升，足以超越它的极速。");
            L3 = this.GetLocalization(nameof(L3), () => "为了能一直跟上您的脚步，我的系统随时准备突破自身的上限。");
        }

        protected override void Build(NarrativeComposer n) {
            n
             .Say("SHPC", L0.Value)
             .Say("SHPC", L1.Value)
             .Reward(ModContent.ItemType<HypersonicBarrelModule>(), 1, string.Empty, blocking: false)
             .Say("SHPC", L2.Value, onEnter: RewardLineAnchor)
             .Say("SHPC", L3.Value, onEnter: PortraitHappy);
        }

        private static void RewardLineAnchor() { }

        protected override bool IsGiftCompleted()
            => ShepelStorySync.ReadGift(d => d.YharonGift, d => d.YharonGift);

        protected override void MarkGiftCompleted()
            => ShepelStorySync.WriteGift(d => d.YharonGift = true, d => d.YharonGift = true);
    }
}

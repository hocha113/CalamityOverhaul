using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Optic;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Shepel.Gifts
{
    internal sealed class ShepelPlanteraGift : ShepelBossGiftNarrative, ILocalizedModType
    {
        public string LocalizationCategory => "ADV.Shepel";

        public static LocalizedText RoleName { get; private set; }
        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }

        public override StyleId DefaultStyle => "SHPC";

        public override int TargetBossId => NPCID.Plantera;

        public override void SetStaticDefaults() {
            RoleName = this.GetLocalization(nameof(RoleName), () => "SHPC");
            L0 = this.GetLocalization(nameof(L0), () => "地下丛林里的藤蔓长得太放肆了，到处都是恼人的倒刺和孢子。");
            L1 = this.GetLocalization(nameof(L1), () => "那朵食人花伪装得很巧妙，但也不过是一堆等待清理的杂草而已。");
            L2 = this.GetLocalization(nameof(L2), () => "我改进了瞄具的动态视觉。这下子，那些挡视线的阔叶就再也无法干扰您的判断了。");
            L3 = this.GetLocalization(nameof(L3), () => "偶尔兼职一下园丁，为您扫清花园里的路障，也是很有趣的工作呢。");
        }

        protected override void Build(NarrativeComposer n) {
            n
             .Say("SHPC", L0.Value)
             .Say("SHPC", L1.Value)
             .SayReward("SHPC", L2.Value, ModContent.ItemType<AdaptiveOpticModule>(), title: string.Empty)
             .Say("SHPC", L3.Value, onEnter: PortraitHappy);
        }

        protected override bool IsGiftCompleted()
            => ShepelStorySync.ReadGift(d => d.PlanteraGift, d => d.PlanteraGift);

        protected override void MarkGiftCompleted()
            => ShepelStorySync.WriteGift(d => d.PlanteraGift = true, d => d.PlanteraGift = true);
    }
}

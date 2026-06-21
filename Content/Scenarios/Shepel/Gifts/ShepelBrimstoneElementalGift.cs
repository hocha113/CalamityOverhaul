using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Barrel;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Shepel.Gifts
{
    internal sealed class ShepelBrimstoneElementalGift : ShepelBossGiftNarrative, ILocalizedModType
    {
        public string LocalizationCategory => "ADV.Shepel";

        public static LocalizedText RoleName { get; private set; }
        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }

        public override StyleId DefaultStyle => "SHPC";

        public override int TargetBossId => CWRID.NPC_BrimstoneElemental;

        public override void SetStaticDefaults() {
            RoleName = this.GetLocalization(nameof(RoleName), () => "SHPC");
            L0 = this.GetLocalization(nameof(L0), () => "呼，那种快要把人烤化的高温终于降下来了。这个女士的脾气可真是火爆。");
            L1 = this.GetLocalization(nameof(L1), () => "让我仔细看看……万幸，您的衣角连一丝被烧焦的痕迹都没有。");
            L2 = this.GetLocalization(nameof(L2), () => "趁着刚才收集到的受热数据，我给您的武器做了一次特别的热处理。");
            L3 = this.GetLocalization(nameof(L3), () => "换上这根全新的枪管吧！下次再遇到粗鲁的家伙，请毫无顾忌地开火，所有的散热工作都包在我身上。");
        }

        protected override void Build(NarrativeComposer n) {
            n
             .Say("SHPC", L0.Value)
             .Say("SHPC", L1.Value)
             .SayReward("SHPC", L2.Value, ModContent.ItemType<MagmaVentBarrelModule>(), title: string.Empty)
             .Say("SHPC", L3.Value, onEnter: PortraitSmirk);
        }

        protected override bool IsGiftCompleted()
            => ShepelStorySync.ReadGift(d => d.BrimstoneElementalGift, d => d.BrimstoneElementalGift);

        protected override void MarkGiftCompleted()
            => ShepelStorySync.WriteGift(d => d.BrimstoneElementalGift = true, d => d.BrimstoneElementalGift = true);
    }
}

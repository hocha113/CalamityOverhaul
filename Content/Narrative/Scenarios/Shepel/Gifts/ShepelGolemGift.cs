using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Stock;
using CalamityOverhaul.Content.Narrative.Scenarios.Shepel;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Narrative.Scenarios.Shepel.Gifts
{
    internal sealed class ShepelGolemGift : ShepelBossGiftNarrative, ILocalizedModType
    {
        public string LocalizationCategory => "ADV.Shepel";

        public static LocalizedText RoleName { get; private set; }
        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }

        public override StyleId DefaultStyle => "SHPC";

        public override int TargetBossId => NPCID.Golem;

        public override void SetStaticDefaults() {
RoleName = this.GetLocalization(nameof(RoleName), () => "SHPC");
            L0 = this.GetLocalization(nameof(L0), () => "神庙里的环境有些压抑。石巨人的攻击虽然笨重，但引发的地形震荡容易破坏射击平衡。");
            L1 = this.GetLocalization(nameof(L1), () => "为了应对这种高频的物理冲击，我对武器架构进行了重新评估。");
            L2 = this.GetLocalization(nameof(L2), () => "我为您的武装加装了最新的减震结构。");
            L3 = this.GetLocalization(nameof(L3), () => "这样即使在剧烈的环境晃动中，您也能保持最稳定的射击姿态。");
        }

        protected override void Build(NarrativeComposer n) {
            n
             .Say("SHPC", L0.Value)
             .Say("SHPC", L1.Value)
             .SayReward("SHPC", L2.Value, ModContent.ItemType<KineticDamperModule>(), title: string.Empty)
             .Say("SHPC", L3.Value);
        }

        protected override bool IsGiftCompleted()
            => ShepelStorySync.ReadGift(d => d.GolemGift, d => d.GolemGift);

        protected override void MarkGiftCompleted()
            => ShepelStorySync.WriteGift(d => d.GolemGift = true, d => d.GolemGift = true);
    }
}

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
    internal sealed class ShepelPerforatorGift : ShepelBossGiftNarrative, ILocalizedModType
    {
        public string LocalizationCategory => "ADV.Shepel";

        public static LocalizedText RoleName { get; private set; }
        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }

        public override StyleId DefaultStyle => "SHPC";

        public override int TargetBossId => CWRID.NPC_PerforatorHive;

        public override void SetStaticDefaults() {
RoleName = this.GetLocalization(nameof(RoleName), () => "SHPC");
            L0 = this.GetLocalization(nameof(L0), () => "这种血肉异形的侵略性极强，行为也充满了野蛮的本能。");
            L1 = this.GetLocalization(nameof(L1), () => "它们经常尝试从视野死角发动攻击，防不胜防。");
            L2 = this.GetLocalization(nameof(L2), () => "为了反制这种突袭，我为您改装了火力网更广阔的散射模块。");
            L3 = this.GetLocalization(nameof(L3), () => "扩大火力覆盖面积，就能在那些威胁靠近您之前提前完成清理。");
        }

        protected override void Build(NarrativeComposer n) {
            n
             .Say("SHPC", L0.Value)
             .Say("SHPC", L1.Value)
             .SayReward("SHPC", L2.Value, ModContent.ItemType<ScattershotBarrelModule>(), title: string.Empty)
             .Say("SHPC", L3.Value);
        }

        protected override bool IsGiftCompleted()
            => ShepelStorySync.ReadGift(d => d.PerforatorGift, d => d.PerforatorGift);

        protected override void MarkGiftCompleted()
            => ShepelStorySync.WriteGift(d => d.PerforatorGift = true, d => d.PerforatorGift = true);
    }
}

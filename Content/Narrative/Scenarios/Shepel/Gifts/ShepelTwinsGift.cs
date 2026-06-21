using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Optic;
using CalamityOverhaul.Content.Narrative.Scenarios.Shepel;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Narrative.Scenarios.Shepel.Gifts
{
    internal sealed class ShepelTwinsGift : ShepelBossGiftNarrative, ILocalizedModType
    {
        public string LocalizationCategory => "ADV.Shepel";

        public static LocalizedText RoleName { get; private set; }
        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }

        public override StyleId DefaultStyle => "SHPC";

        public override int TargetBossId => NPCID.Retinazer;

        public override void SetStaticDefaults() {
RoleName = this.GetLocalization(nameof(RoleName), () => "SHPC");
            L0 = this.GetLocalization(nameof(L0), () => "机械双子眼的交叉火力覆盖相当严密。");
            L1 = this.GetLocalization(nameof(L1), () => "不过，只要我们的链接保持稳定，就不会给它们留下任何死角。");
            L2 = this.GetLocalization(nameof(L2), () => "我强化了瞄准系统的二次扫描能力，提升了锁定的可靠性。");
            L3 = this.GetLocalization(nameof(L3), () => "请放心把侧翼和背后交给我。");
        }

        protected override void Build(NarrativeComposer n) {
            n
             .Say("SHPC", L0.Value)
             .Say("SHPC", L1.Value)
             .SayReward("SHPC", L2.Value, ModContent.ItemType<PrecisionOpticModule>(), title: string.Empty)
             .Say("SHPC", L3.Value);
        }

        protected override bool IsGiftCompleted()
            => ShepelStorySync.ReadGift(d => d.TwinsGift, d => d.TwinsGift);

        protected override void MarkGiftCompleted()
            => ShepelStorySync.WriteGift(d => d.TwinsGift = true, d => d.TwinsGift = true);

        protected override bool AdditionalConditions(Player player)
            => !NPC.AnyNPCs(NPCID.Spazmatism);
    }
}

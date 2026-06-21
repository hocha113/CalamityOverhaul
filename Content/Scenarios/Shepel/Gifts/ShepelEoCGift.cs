using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Barrel;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Shepel.Gifts
{
    internal sealed class ShepelEoCGift : ShepelBossGiftNarrative, ILocalizedModType
    {
        public string LocalizationCategory => "ADV.Shepel";

        public static LocalizedText RoleName { get; private set; }
        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }

        public override StyleId DefaultStyle => "SHPC";

        public override int TargetBossId => NPCID.EyeofCthulhu;

        public override void SetStaticDefaults() {
            RoleName = this.GetLocalization(nameof(RoleName), () => "SHPC");
            L0 = this.GetLocalization(nameof(L0), () => "夜晚的视线有些模糊呢，主人。不过那只巨大的眼睛已经被您彻底击碎了。");
            L1 = this.GetLocalization(nameof(L1), () => "我已经为您准备好了热茶和干净的毛巾，请稍作休整。");
            L2 = this.GetLocalization(nameof(L2), () => "在您休息时，我会对武器的光学系统进行改进，光束会更加聚拢。");
            L3 = this.GetLocalization(nameof(L3), () => "这只是旅途的开始。无论接下来去哪，我都会为您打理好一切后勤与武装。");
        }

        protected override void Build(NarrativeComposer n) {
            n
             .Say("SHPC", L0.Value)
             .Say("SHPC", L1.Value)
             .SayReward("SHPC", L2.Value, ModContent.ItemType<LaserBarrelModule>(), title: string.Empty)
             .Say("SHPC", L3.Value);
        }

        protected override bool IsGiftCompleted()
            => ShepelStorySync.ReadGift(d => d.EyeOfCthulhuGift, d => d.EyeOfCthulhuGift);

        protected override void MarkGiftCompleted()
            => ShepelStorySync.WriteGift(d => d.EyeOfCthulhuGift = true, d => d.EyeOfCthulhuGift = true);
    }
}

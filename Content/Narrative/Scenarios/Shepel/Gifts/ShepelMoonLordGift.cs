using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Power;
using CalamityOverhaul.Content.Narrative.Scenarios.Shepel;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Narrative.Scenarios.Shepel.Gifts
{
    internal sealed class ShepelMoonLordGift : ShepelBossGiftNarrative, ILocalizedModType
    {
        public string LocalizationCategory => "ADV.Shepel";

        public static LocalizedText RoleName { get; private set; }
        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }

        public override StyleId DefaultStyle => "SHPC";

        public override int TargetBossId => NPCID.MoonLordCore;

        public override void SetStaticDefaults() {
RoleName = this.GetLocalization(nameof(RoleName), () => "SHPC");
            L0 = this.GetLocalization(nameof(L0), () => "盘踞在星空之上的异形终于陨落了。");
            L1 = this.GetLocalization(nameof(L1), () => "确认主人生理体征平稳……太好了，警报解除。");
            L2 = this.GetLocalization(nameof(L2), () => "我从它残余的高维力场中提炼出了这个核心。");
            L3 = this.GetLocalization(nameof(L3), () => "无论前方的道路通向多远的深空，我都会作为您的护盾与向导，绝不偏航。");
        }

        protected override void Build(NarrativeComposer n) {
            n
             .Say("SHPC", L0.Value)
             .Say("SHPC", L1.Value)
             .Reward(ModContent.ItemType<SingularityCoreModule>(), 1, string.Empty, blocking: false)
             .Say("SHPC", L2.Value, onEnter: RewardLineAnchor)
             .Say("SHPC", L3.Value, onEnter: PortraitHappy);
        }

        private static void RewardLineAnchor() { }

        protected override bool IsGiftCompleted()
            => ShepelStorySync.ReadGift(d => d.MoonLordGift, d => d.MoonLordGift);

        protected override void MarkGiftCompleted()
            => ShepelStorySync.WriteGift(d => d.MoonLordGift = true, d => d.MoonLordGift = true);
    }
}

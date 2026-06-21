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
    internal sealed class ShepelAquaticScourgeGift : ShepelBossGiftNarrative, ILocalizedModType
    {
        public string LocalizationCategory => "ADV.Shepel";

        public static LocalizedText RoleName { get; private set; }
        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }

        public override StyleId DefaultStyle => "SHPC";

        public override int TargetBossId => CWRID.NPC_AquaticScourgeHead;

        public override void SetStaticDefaults() {
RoleName = this.GetLocalization(nameof(RoleName), () => "SHPC");
            L0 = this.GetLocalization(nameof(L0), () => "主人，渊海的水质可真让人头疼。这些飘来飘去的浑浊杂质，实在是太碍眼了。");
            L1 = this.GetLocalization(nameof(L1), () => "刚才飞溅的酸液弄脏了您的装甲，不过请放心，我已经抢先一步为您清理得干干净净了。");
            L2 = this.GetLocalization(nameof(L2), () => "另外，为了不让这些深海环境再来捣乱，我稍微花了一点心思，给瞄准镜重新写了一套光学滤镜。");
            L3 = this.GetLocalization(nameof(L3), () => "毕竟，让您在任何地方都能保持清爽完美的视野，是我最引以为傲的工作之一呢。");
        }

        protected override void Build(NarrativeComposer n) {
            n
             .Say("SHPC", L0.Value)
             .Say("SHPC", L1.Value)
             .Reward(ModContent.ItemType<HoloOpticModule>(), 1, string.Empty, blocking: false)
             .Say("SHPC", L2.Value, onEnter: RewardLineAnchor)
             .Say("SHPC", L3.Value);
        }

        private static void RewardLineAnchor() { }

        protected override bool IsGiftCompleted()
            => ShepelStorySync.ReadGift(d => d.AquaticScourgeGift, d => d.AquaticScourgeGift);

        protected override void MarkGiftCompleted()
            => ShepelStorySync.WriteGift(d => d.AquaticScourgeGift = true, d => d.AquaticScourgeGift = true);
    }
}

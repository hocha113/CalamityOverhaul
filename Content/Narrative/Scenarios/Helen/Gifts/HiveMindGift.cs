using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Narrative.Scenarios.Helen;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Narrative.Scenarios.Helen.Gifts
{
    internal sealed class HiveMindGift : HelenBossGiftNarrative, ILocalizedModType
    {
        public string LocalizationCategory => "ADV";

        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }
        public static LocalizedText L4 { get; private set; }
        public static LocalizedText R1 { get; private set; }

        public override StyleId DefaultStyle => "Sea";

        public override int TargetBossId => CWRID.NPC_HiveMind;

        public override void SetStaticDefaults() {
            R1 = this.GetLocalization(nameof(R1), () => "比目鱼");
            L0 = this.GetLocalization(nameof(L0), () => "一个由腐败构成的集体意识……真是恶心的概念");
            L1 = this.GetLocalization(nameof(L1), () => "它的思维方式一定很特别，无数腐烂的碎片拼凑成一个扭曲的整体");
            L2 = this.GetLocalization(nameof(L2), () => "给，腐烂鱼。从那堆腐肉里捞出来的，别问我怎么做到的");
            L3 = this.GetLocalization(nameof(L3), () => "虽然闻起来像是被遗忘在阳光下三天的海鲜");
            L4 = this.GetLocalization(nameof(L4), () => "但据说它能让人产生一种……与腐败共鸣的感觉。听起来就不太对劲");
        }

        protected override void Build(NarrativeComposer n) {
            n
             .Say("Helen", "SlightAnnoyed", L0.Value)
             .Say("Helen", "SlightAnnoyed", L1.Value)
             .Reward(ItemID.EaterofPlankton, 1, string.Empty, blocking: false)
             .Say("Helen", L2.Value, onEnter: RewardLineAnchor)
             .Say("Helen", "Solemn", L3.Value)
             .Say("Helen", "Solemn", L4.Value);
        }

        private static void RewardLineAnchor() { }

        protected override bool IsGiftCompleted()
            => HalibutStorySync.ReadGift(d => d.HiveMindGift, d => d.HiveMindGift);

        protected override void MarkGiftCompleted()
            => HalibutStorySync.WriteGift(d => d.HiveMindGift = true, d => d.HiveMindGift = true);
    }
}

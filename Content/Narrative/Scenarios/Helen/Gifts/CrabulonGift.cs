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
    internal sealed class CrabulonGift : HelenBossGiftNarrative, ILocalizedModType
    {
        public string LocalizationCategory => "ADV";

        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }
        public static LocalizedText L4 { get; private set; }
        public static LocalizedText R1 { get; private set; }

        public override StyleId DefaultStyle => "Sea";

        public override int TargetBossId => CWRID.NPC_Crabulon;

        public override void SetStaticDefaults() {
            R1 = this.GetLocalization(nameof(R1), () => "比目鱼");
            L0 = this.GetLocalization(nameof(L0), () => "那团蘑菇状的……生物？它的菌盖下藏着的究竟是智慧还是本能");
            L1 = this.GetLocalization(nameof(L1), () => "你知道吗，蘑菇的菌丝网络可以传递信息，也许它刚才在向同伴求救");
            L2 = this.GetLocalization(nameof(L2), () => "这是蘑菇鱼，混在从它身上散发出的孢子里，我好不容易逮到的");
            L3 = this.GetLocalization(nameof(L3), () => "别担心，这些孢子不会让人类变成菌类……大概");
            L4 = this.GetLocalization(nameof(L4), () => "不过如果你突然想在阴暗潮湿的地方扎根，记得告诉我");
        }

        protected override void Build(NarrativeComposer n) {
            n
             .Say("Helen", "Solemn", L0.Value)
             .Say("Helen", "Solemn", L1.Value)
             .Reward(ItemID.AmanitaFungifin, 1, string.Empty, blocking: false)
             .Say("Helen", L2.Value, onEnter: RewardLineAnchor)
             .Say("Helen", "Naughty", L3.Value)
             .Say("Helen", "Naughty", L4.Value);
        }

        private static void RewardLineAnchor() { }

        protected override bool IsGiftCompleted()
            => HalibutStorySync.ReadGift(d => d.CrabulonGift, d => d.CrabulonGift);

        protected override void MarkGiftCompleted()
            => HalibutStorySync.WriteGift(d => d.CrabulonGift = true, d => d.CrabulonGift = true);
    }
}

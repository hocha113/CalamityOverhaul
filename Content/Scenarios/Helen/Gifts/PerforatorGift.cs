using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Helen.Gifts
{
    internal sealed class PerforatorGift : HelenBossGiftNarrative, ILocalizedModType
    {
        public string LocalizationCategory => "ADV";

        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }
        public static LocalizedText L4 { get; private set; }

        public override StyleId DefaultStyle => "Sea";

        public override int TargetBossId => CWRID.NPC_PerforatorHive;

        public override void SetStaticDefaults() {
            L0 = this.GetLocalization(nameof(L0), () => "一堆会钻孔的血肉虫……这种生物的存在本身就是对生物学的挑战");
            L1 = this.GetLocalization(nameof(L1), () => "它们的血液有种奇特的粘稠度，像是某种活体金属");
            L2 = this.GetLocalization(nameof(L2), () => "我从残骸里找到了这个，血腥鱼。它还在蠕动");
            L3 = this.GetLocalization(nameof(L3), () => "别被它的外表吓到，虽然看起来像是从噩梦里爬出来的");
            L4 = this.GetLocalization(nameof(L4), () => "但至少它不会在你睡觉时钻进你的耳朵……应该不会");
        }

        protected override void Build(NarrativeComposer n) {
            n
             .Say("Helen", "Stern", L0.Value)
             .Say("Helen", "Stern", L1.Value)
             .SayReward("Helen", L2.Value, ItemID.BloodyManowar, title: string.Empty)
             .Say("Helen", L3.Value)
             .Say("Helen", "Solemn", L4.Value);
        }

        protected override bool IsGiftCompleted()
            => HalibutStorySync.ReadGift(d => d.PerforatorGift, d => d.PerforatorGift);

        protected override void MarkGiftCompleted()
            => HalibutStorySync.WriteGift(d => d.PerforatorGift = true, d => d.PerforatorGift = true);
    }
}

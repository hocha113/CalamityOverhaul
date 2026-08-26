using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Helen.Gifts
{
    internal sealed class PlaguebringerGift : HelenBossGiftNarrative, ILocalizedModType
    {
        public string LocalizationCategory => "ADV.Helen";

        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }
        public static LocalizedText L4 { get; private set; }

        public override StyleId DefaultStyle => "Sea";

        public override int TargetBossId => CWRID.NPC_PlaguebringerGoliath;

        public override void SetStaticDefaults() {
            L0 = this.GetLocalization(nameof(L0), () => "瘟疫的使者……一只机械蜜蜂携带着生物武器。谁设计的这个？一定是个混蛋，竟敢奴役蜜蜂");
            L1 = this.GetLocalization(nameof(L1), () => "病毒不在乎对错，它只是想活下去。就像我们一样，只是手段更直接");
            L2 = this.GetLocalization(nameof(L2), () => "珠宝鱼，被感染过又奇迹般痊愈的鱼。它的免疫系统比你的人生经历还丰富");
            L3 = this.GetLocalization(nameof(L3), () => "据说吃了它能增强抵抗力。但我觉得这更像是'杀不死你的让你更强'的另一种说法");
            L4 = this.GetLocalization(nameof(L4), () => "不过既然我们刚从瘟疫蜂窝里走出来，这点小风险应该不算什么");
        }

        protected override void Build(NarrativeComposer n) {
            n
             .Say("Helen", "Enjoy", L0.Value)
             .Say("Helen", L1.Value)
             .SayReward("Helen", L2.Value, ItemID.Jewelfish, title: string.Empty)
             .Say("Helen", L3.Value)
             .Say("Helen", L4.Value);
        }

        protected override bool IsGiftCompleted()
            => HalibutStorySync.ReadGift(d => d.PlaguebringerGift, d => d.PlaguebringerGift);

        protected override void MarkGiftCompleted()
            => HalibutStorySync.WriteGift(d => d.PlaguebringerGift = true, d => d.PlaguebringerGift = true);
    }
}

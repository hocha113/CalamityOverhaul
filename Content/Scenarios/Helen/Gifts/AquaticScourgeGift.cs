using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Helen.Gifts
{
    internal sealed class AquaticScourgeGift : HelenBossGiftNarrative, ILocalizedModType
    {
        public string LocalizationCategory => "ADV";

        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }
        public static LocalizedText L4 { get; private set; }

        public override StyleId DefaultStyle => "Sea";

        public override int TargetBossId => CWRID.NPC_AquaticScourgeHead;

        public override void SetStaticDefaults() {
            L0 = this.GetLocalization(nameof(L0), () => "它让我想起家乡那些不太友好的邻居，不过它倒是挺友善的，它曾经在硫磺海大学里担任过保安队长，经常和我打招呼");
            L1 = this.GetLocalization(nameof(L1), () => "硫磺海是个有趣的地方，那里的生物都在问同一个问题：'为什么我还活着？'");
            L2 = this.GetLocalization(nameof(L2), () => "棱镜鱼，它的鳞片能折射出你从未见过的颜色。主要是因为它们不该存在");
            L3 = this.GetLocalization(nameof(L3), () => "盯着它看会让你的视觉系统重启，有点像强制更新，但更痛苦");
            L4 = this.GetLocalization(nameof(L4), () => "最好在光线暗淡的地方使用，不然感觉会像嗑了似的");
        }

        protected override void Build(NarrativeComposer n) {
            n
             .Say("Helen", "Enjoy", L0.Value)
             .Say("Helen", L1.Value)
             .SayReward("Helen", L2.Value, ItemID.Prismite, title: string.Empty)
             .Say("Helen", L3.Value)
             .Say("Helen", L4.Value);
        }

        protected override bool IsGiftCompleted()
            => HalibutStorySync.ReadGift(d => d.AquaticScourgeGift, d => d.AquaticScourgeGift);

        protected override void MarkGiftCompleted()
            => HalibutStorySync.WriteGift(d => d.AquaticScourgeGift = true, d => d.AquaticScourgeGift = true);
    }
}

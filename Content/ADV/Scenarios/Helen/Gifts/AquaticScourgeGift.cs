using CalamityOverhaul.Content.ADV.ADVRewardPopups;
using CalamityOverhaul.Content.ADV.Common;
using CalamityOverhaul.Content.ADV.DialogueBoxs;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Helen.Gifts
{
    internal class AquaticScourgeGift : GiftScenarioBase
    {
        public override string Key => nameof(AquaticScourgeGift);
        public override int TargetBossID => CWRID.NPC_AquaticScourgeHead;
        public static LocalizedText R1 { get; private set; }
        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }
        public static LocalizedText L4 { get; private set; }
        public override void SetStaticDefaults() {
            R1 = this.GetLocalization(nameof(R1), () => "比目鱼");
            L0 = this.GetLocalization(nameof(L0), () => "它让我想起家乡那些不太友好的邻居，不过它倒是挺友善的，它曾经在硫磺海大学里担任过保安队长，经常和我打招呼");
            L1 = this.GetLocalization(nameof(L1), () => "硫磺海是个有趣的地方，那里的生物都在问同一个问题：'为什么我还活着？'");
            L2 = this.GetLocalization(nameof(L2), () => "棱镜鱼，它的鳞片能折射出你从未见过的颜色。主要是因为它们不该存在");
            L3 = this.GetLocalization(nameof(L3), () => "盯着它看会让你的视觉系统重启，有点像强制更新，但更痛苦");
            L4 = this.GetLocalization(nameof(L4), () => "最好在光线暗淡的地方使用，不然感觉会像嗑了似的");
        }
        protected override void Build() {
            DialogueBoxBase.RegisterPortrait(R1.Value, ADVAsset.HelenADV);
            DialogueBoxBase.RegisterPortrait(R1.Value + " ", ADVAsset.Helen_doubtADV);
            Add(R1.Value + " ", L0.Value);
            Add(R1.Value, L1.Value);
            AddReward(R1.Value, L2.Value, ItemID.Prismite);
            Add(R1.Value, L3.Value);
            Add(R1.Value, L4.Value);
        }
        protected override bool IsGiftCompleted(ADVSave save) {
            return save.Get<BossGiftADVData>().AquaticScourgeGift;
        }
        protected override void MarkGiftCompleted(ADVSave save) {
            save.Get<BossGiftADVData>().AquaticScourgeGift = true;
        }
        protected override bool StartScenarioInternal() {
            return ScenarioManager.Start<AquaticScourgeGift>();
        }
    }
}

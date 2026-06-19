using CalamityOverhaul.Content.ADV.ADVRewardPopups;
using CalamityOverhaul.Content.ADV.Common;
using CalamityOverhaul.Content.ADV.DialogueBoxs;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Helen.Gifts
{
    internal class SlimeGodGift : GiftScenarioBase
    {
        public override string Key => nameof(SlimeGodGift);
        public override int TargetBossID => CWRID.NPC_SlimeGodCore;
        public static LocalizedText R1 { get; private set; }
        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }
        public static LocalizedText L4 { get; private set; }
        public override void SetStaticDefaults() {
            R1 = this.GetLocalization(nameof(R1), () => "比目鱼");
            L0 = this.GetLocalization(nameof(L0), () => "三个史莱姆，呃，或者说一个分裂的神性？这种二元对立的存在形式很有意思");
            L1 = this.GetLocalization(nameof(L1), () => "腐化与猩红，就像这个世界永恒的矛盾。而我们刚才证明了矛盾可以被'解决'");
            L2 = this.GetLocalization(nameof(L2), () => "这是杂色猪油鱼，从那堆粘液里捞出来的。别问我为什么叫这个名字");
            L3 = this.GetLocalization(nameof(L3), () => "它的颜色会随着观察者的心情改变，或许它在模仿人内心的混乱");
            L4 = this.GetLocalization(nameof(L4), () => "如果你盯着它看太久，可能会开始思考自己到底属于哪一边");
        }
        protected override void Build() {
            DialogueBoxBase.RegisterPortrait(R1.Value, ADVAsset.HelenADV);
            DialogueBoxBase.RegisterPortrait(R1.Value + " ", ADVAsset.Helen_solemnADV);
            Add(R1.Value, L0.Value);
            Add(R1.Value, L1.Value);
            AddReward(R1.Value + " ", L2.Value, ItemID.VariegatedLardfish); //奖励
            Add(R1.Value, L3.Value);
            Add(R1.Value, L4.Value);
        }
        protected override bool IsGiftCompleted(ADVSave save) {
            return save.Get<BossGiftADVData>().SlimeGodGift;
        }
        protected override void MarkGiftCompleted(ADVSave save) {
            save.Get<BossGiftADVData>().SlimeGodGift = true;
        }
        protected override bool StartScenarioInternal() {
            return ScenarioManager.Start<SlimeGodGift>();
        }
    }
}

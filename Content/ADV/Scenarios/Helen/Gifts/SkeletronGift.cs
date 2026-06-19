using CalamityOverhaul.Content.ADV.ADVRewardPopups;
using CalamityOverhaul.Content.ADV.Common;
using CalamityOverhaul.Content.ADV.DialogueBoxs;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Helen.Gifts
{
    internal class SkeletronGift : GiftScenarioBase
    {
        public override string Key => nameof(SkeletronGift);
        public override int TargetBossID => NPCID.SkeletronHead;
        public static LocalizedText R1 { get; private set; }
        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }
        public static LocalizedText L4 { get; private set; }
        public override void SetStaticDefaults() {
            R1 = this.GetLocalization(nameof(R1), () => "比目鱼");
            L0 = this.GetLocalization(nameof(L0), () => "那可真是一大堆钙质!");
            L1 = this.GetLocalization(nameof(L1), () => "那东西的颅骨结构，让我想起一只失控的意念聚合体");
            L2 = this.GetLocalization(nameof(L2), () => "让我枪管冷却一下，我刚才从这周围捡到了一条鱼");
            L3 = this.GetLocalization(nameof(L3), () => "你看，这是‘骷髅王鱼’，据说它体内的磷质能让夜钓的人思考人生");
            L4 = this.GetLocalization(nameof(L4), () => "走吧，前面还有更抽象的骨头在等着我们");
        }
        protected override void Build() {
            DialogueBoxBase.RegisterPortrait(R1.Value, ADVAsset.HelenADV);
            DialogueBoxBase.RegisterPortrait(R1.Value + " ", ADVAsset.Helen_enjoyADV);
            Add(R1.Value, L0.Value);
            Add(R1.Value + " ", L1.Value);
            Add(R1.Value + " ", L2.Value);
            AddReward(R1.Value, L3.Value, ItemID.Fishotron); //奖励
            Add(R1.Value, L4.Value);
        }
        protected override bool IsGiftCompleted(ADVSave save) {
            return save.Get<BossGiftADVData>().SkeletronGift;
        }
        protected override void MarkGiftCompleted(ADVSave save) {
            save.Get<BossGiftADVData>().SkeletronGift = true;
        }
        protected override bool StartScenarioInternal() {
            return ScenarioManager.Start<SkeletronGift>();
        }
    }
}

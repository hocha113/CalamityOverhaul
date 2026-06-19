using CalamityOverhaul.Content.ADV.ADVRewardPopups;
using CalamityOverhaul.Content.ADV.Common;
using CalamityOverhaul.Content.ADV.DialogueBoxs;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Helen.Gifts
{
    internal class ProvidenceGift : GiftScenarioBase
    {
        public override string Key => nameof(ProvidenceGift);
        public override int TargetBossID => CWRID.NPC_Providence;
        public static LocalizedText R1 { get; private set; }
        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }
        public static LocalizedText L4 { get; private set; }
        public override void SetStaticDefaults() {
            R1 = this.GetLocalization(nameof(R1), () => "比目鱼");
            L0 = this.GetLocalization(nameof(L0), () => "亵渎天神……一个由信仰和火焰构成的矛盾体。挺可怜的");
            L1 = this.GetLocalization(nameof(L1), () => "我们刚才熄灭的不仅是圣火，还有一个纪元的余烬");
            L2 = this.GetLocalization(nameof(L2), () => "恶魔地狱鱼，从她的灰烬中重生的。它的温度永远保持在'刚好不会烫伤你'的程度");
            L3 = this.GetLocalization(nameof(L3), () => "这种精确控制让我怀疑，也许它只是想被理解");
            L4 = this.GetLocalization(nameof(L4), () => "不过理解和战争之间的界限，只是一次攻击的距离");
        }
        protected override void Build() {
            DialogueBoxBase.RegisterPortrait(R1.Value, ADVAsset.HelenADV);
            Add(R1.Value, L0.Value);
            Add(R1.Value, L1.Value);
            AddReward(R1.Value, L2.Value, ItemID.DemonicHellfish); //奖励
            Add(R1.Value, L3.Value);
            Add(R1.Value, L4.Value);
        }
        protected override bool IsGiftCompleted(ADVSave save) {
            return save.Get<BossGiftADVData>().ProvidenceGift;
        }
        protected override void MarkGiftCompleted(ADVSave save) {
            save.Get<BossGiftADVData>().ProvidenceGift = true;
        }
        protected override bool StartScenarioInternal() {
            return ScenarioManager.Start<ProvidenceGift>();
        }
    }
}

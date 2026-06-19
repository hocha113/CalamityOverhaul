using CalamityOverhaul.Content.ADV.ADVRewardPopups;
using CalamityOverhaul.Content.ADV.Common;
using CalamityOverhaul.Content.ADV.DialogueBoxs;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Helen.Gifts
{
    internal class CrabulonGift : GiftScenarioBase
    {
        public override string Key => nameof(CrabulonGift);
        public override int TargetBossID => CWRID.NPC_Crabulon;
        public static LocalizedText R1 { get; private set; }
        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }
        public static LocalizedText L4 { get; private set; }
        public override void SetStaticDefaults() {
            R1 = this.GetLocalization(nameof(R1), () => "比目鱼");
            L0 = this.GetLocalization(nameof(L0), () => "那团蘑菇状的……生物？它的菌盖下藏着的究竟是智慧还是本能");
            L1 = this.GetLocalization(nameof(L1), () => "你知道吗，蘑菇的菌丝网络可以传递信息，也许它刚才在向同伴求救");
            L2 = this.GetLocalization(nameof(L2), () => "这是蘑菇鱼，混在从它身上散发出的孢子里，我好不容易逮到的");
            L3 = this.GetLocalization(nameof(L3), () => "别担心，这些孢子不会让人类变成菌类……大概");
            L4 = this.GetLocalization(nameof(L4), () => "不过如果你突然想在阴暗潮湿的地方扎根，记得告诉我");
        }
        protected override void Build() {
            DialogueBoxBase.RegisterPortrait(R1.Value, ADVAsset.HelenADV);
            DialogueBoxBase.RegisterPortrait(R1.Value + " ", ADVAsset.Helen_solemnADV);
            DialogueBoxBase.RegisterPortrait(R1.Value + " " + " ", ADVAsset.Helen_naughtyADV);
            Add(R1.Value + " ", L0.Value);
            Add(R1.Value + " ", L1.Value);
            AddReward(R1.Value, L2.Value, ItemID.AmanitaFungifin); //奖励
            Add(R1.Value + " " + " ", L3.Value);
            Add(R1.Value + " " + " ", L4.Value);
        }
        protected override bool IsGiftCompleted(ADVSave save) {
            return save.Get<BossGiftADVData>().CrabulonGift;
        }
        protected override void MarkGiftCompleted(ADVSave save) {
            save.Get<BossGiftADVData>().CrabulonGift = true;
        }
        protected override bool StartScenarioInternal() {
            return ScenarioManager.Start<CrabulonGift>();
        }
    }
}

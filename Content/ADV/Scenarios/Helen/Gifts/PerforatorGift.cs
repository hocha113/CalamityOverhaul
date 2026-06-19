using CalamityOverhaul.Content.ADV.ADVRewardPopups;
using CalamityOverhaul.Content.ADV.Common;
using CalamityOverhaul.Content.ADV.DialogueBoxs;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Helen.Gifts
{
    internal class PerforatorGift : GiftScenarioBase
    {
        public override string Key => nameof(PerforatorGift);
        public override int TargetBossID => CWRID.NPC_PerforatorHive;
        public static LocalizedText R1 { get; private set; }
        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }
        public static LocalizedText L4 { get; private set; }
        public override void SetStaticDefaults() {
            R1 = this.GetLocalization(nameof(R1), () => "比目鱼");
            L0 = this.GetLocalization(nameof(L0), () => "一堆会钻孔的血肉虫……这种生物的存在本身就是对生物学的挑战");
            L1 = this.GetLocalization(nameof(L1), () => "它们的血液有种奇特的粘稠度，像是某种活体金属");
            L2 = this.GetLocalization(nameof(L2), () => "我从残骸里找到了这个，血腥鱼。它还在蠕动");
            L3 = this.GetLocalization(nameof(L3), () => "别被它的外表吓到，虽然看起来像是从噩梦里爬出来的");
            L4 = this.GetLocalization(nameof(L4), () => "但至少它不会在你睡觉时钻进你的耳朵……应该不会");
        }
        protected override void Build() {
            DialogueBoxBase.RegisterPortrait(R1.Value, ADVAsset.HelenADV);
            DialogueBoxBase.RegisterPortrait(R1.Value + " ", ADVAsset.Helen_seriousADV);
            DialogueBoxBase.RegisterPortrait(R1.Value + " " + " ", ADVAsset.Helen_solemnADV);
            Add(R1.Value + " ", L0.Value);
            Add(R1.Value + " ", L1.Value);
            AddReward(R1.Value, L2.Value, ItemID.BloodyManowar); //奖励
            Add(R1.Value, L3.Value);
            Add(R1.Value + " " + " ", L4.Value);
        }
        protected override bool IsGiftCompleted(ADVSave save) {
            return save.Get<BossGiftADVData>().PerforatorGift;
        }
        protected override void MarkGiftCompleted(ADVSave save) {
            save.Get<BossGiftADVData>().PerforatorGift = true;
        }
        protected override bool StartScenarioInternal() {
            return ScenarioManager.Start<PerforatorGift>();
        }
    }
}

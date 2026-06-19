using CalamityOverhaul.Content.ADV.ADVRewardPopups;
using CalamityOverhaul.Content.ADV.Common;
using CalamityOverhaul.Content.ADV.DialogueBoxs;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Helen.Gifts
{
    internal class HiveMindGift : GiftScenarioBase
    {
        public override string Key => nameof(HiveMindGift);
        public override int TargetBossID => CWRID.NPC_HiveMind;
        public static LocalizedText R1 { get; private set; }
        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }
        public static LocalizedText L4 { get; private set; }
        public override void SetStaticDefaults() {
            R1 = this.GetLocalization(nameof(R1), () => "比目鱼");
            L0 = this.GetLocalization(nameof(L0), () => "一个由腐败构成的集体意识……真是恶心的概念");
            L1 = this.GetLocalization(nameof(L1), () => "它的思维方式一定很特别，无数腐烂的碎片拼凑成一个扭曲的整体");
            L2 = this.GetLocalization(nameof(L2), () => "给，腐烂鱼。从那堆腐肉里捞出来的，别问我怎么做到的");
            L3 = this.GetLocalization(nameof(L3), () => "虽然闻起来像是被遗忘在阳光下三天的海鲜");
            L4 = this.GetLocalization(nameof(L4), () => "但据说它能让人产生一种……与腐败共鸣的感觉。听起来就不太对劲");
        }
        protected override void Build() {
            DialogueBoxBase.RegisterPortrait(R1.Value, ADVAsset.HelenADV);
            DialogueBoxBase.RegisterPortrait(R1.Value + " ", ADVAsset.Helen_slightAnnoyedADV);
            DialogueBoxBase.RegisterPortrait(R1.Value + " " + " ", ADVAsset.Helen_solemnADV);
            Add(R1.Value + " ", L0.Value);
            Add(R1.Value + " ", L1.Value);
            AddReward(R1.Value, L2.Value, ItemID.EaterofPlankton); //奖励
            Add(R1.Value + " " + " ", L3.Value);
            Add(R1.Value + " " + " ", L4.Value);
        }
        protected override bool IsGiftCompleted(ADVSave save) {
            return save.Get<BossGiftADVData>().HiveMindGift;
        }
        protected override void MarkGiftCompleted(ADVSave save) {
            save.Get<BossGiftADVData>().HiveMindGift = true;
        }
        protected override bool StartScenarioInternal() {
            return ScenarioManager.Start<HiveMindGift>();
        }
    }
}

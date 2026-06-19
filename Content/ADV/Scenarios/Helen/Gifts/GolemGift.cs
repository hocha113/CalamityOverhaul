using CalamityOverhaul.Content.ADV.ADVRewardPopups;
using CalamityOverhaul.Content.ADV.Common;
using CalamityOverhaul.Content.ADV.DialogueBoxs;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Helen.Gifts
{
    internal class GolemGift : GiftScenarioBase
    {
        public override string Key => nameof(GolemGift);
        public override int TargetBossID => NPCID.Golem;
        public static LocalizedText R1 { get; private set; }
        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }
        public static LocalizedText L4 { get; private set; }
        public override void SetStaticDefaults() {
            R1 = this.GetLocalization(nameof(R1), () => "比目鱼");
            L0 = this.GetLocalization(nameof(L0), () => "一堆会动的石头，古代文明的遗产，或者说是他们失败的证明");
            L1 = this.GetLocalization(nameof(L1), () => "它可能在守护着什么，或者仅仅是在重复一个早已失去意义的程序");
            L2 = this.GetLocalization(nameof(L2), () => "岩鱼，从神殿的地基里挖出来的。它的密度高到让我怀疑人生");
            L3 = this.GetLocalization(nameof(L3), () => "我以前试着煮过它，但我的锅先投降了");
            L4 = this.GetLocalization(nameof(L4), () => "......有些东西存在的意义就是让人意识到，并非所有问题都需要被解决");
        }
        protected override void Build() {
            DialogueBoxBase.RegisterPortrait(R1.Value, ADVAsset.HelenADV);
            DialogueBoxBase.RegisterPortrait(R1.Value + " ", ADVAsset.Helen_solemnADV);
            DialogueBoxBase.RegisterPortrait(R1.Value + " " + " ", ADVAsset.Helen_seriousADV);
            Add(R1.Value + " ", L0.Value);
            Add(R1.Value + " ", L1.Value);
            AddReward(R1.Value, L2.Value, ItemID.Rockfish); //奖励
            Add(R1.Value + " " + " ", L3.Value);
            Add(R1.Value + " " + " ", L4.Value);
        }
        protected override bool IsGiftCompleted(ADVSave save) {
            return save.Get<BossGiftADVData>().GolemGift;
        }
        protected override void MarkGiftCompleted(ADVSave save) {
            save.Get<BossGiftADVData>().GolemGift = true;
        }
        protected override bool StartScenarioInternal() {
            return ScenarioManager.Start<GolemGift>();
        }
    }
}

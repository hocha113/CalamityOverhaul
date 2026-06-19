using CalamityOverhaul.Content.ADV.ADVRewardPopups;
using CalamityOverhaul.Content.ADV.Common;
using CalamityOverhaul.Content.ADV.DialogueBoxs;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Helen.Gifts
{
    internal class YharonGift : GiftScenarioBase
    {
        public override string Key => nameof(YharonGift);
        public override int TargetBossID => CWRID.NPC_Yharon;
        public static LocalizedText R1 { get; private set; }
        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }
        public static LocalizedText L4 { get; private set; }
        public override void SetStaticDefaults() {
            R1 = this.GetLocalization(nameof(R1), () => "比目鱼");
            L0 = this.GetLocalization(nameof(L0), () => "丛林龙，嗯......应该叫它焚世之龙，它燃烧的并非肉体，而是执念");
            L1 = this.GetLocalization(nameof(L1), () => "忠诚到愿意为主人燃尽自己，这种纯粹让我想起海底那些守护珊瑚礁的鱼群");
            L2 = this.GetLocalization(nameof(L2), () => "猩红虎鱼，刚才逮到的，我很喜欢它身上的条纹");
            L3 = this.GetLocalization(nameof(L3), () => "握着它会感觉到一种灼热的决心，那是属于战士的温度");
            L4 = this.GetLocalization(nameof(L4), () => "你击败了那条龙，但我怀疑……它在倒下的瞬间，是否终于获得了解脱");
        }
        protected override void Build() {
            DialogueBoxBase.RegisterPortrait(R1.Value, ADVAsset.HelenADV);
            DialogueBoxBase.RegisterPortrait(R1.Value + " ", ADVAsset.Helen_solemnADV);
            Add(R1.Value + " ", L0.Value);
            Add(R1.Value, L1.Value);
            AddReward(R1.Value, L2.Value, ItemID.CrimsonTigerfish); //奖励
            Add(R1.Value, L3.Value);
            Add(R1.Value, L4.Value);
        }
        protected override bool IsGiftCompleted(ADVSave save) {
            return save.Get<BossGiftADVData>().YharonGift;
        }
        protected override void MarkGiftCompleted(ADVSave save) {
            save.Get<BossGiftADVData>().YharonGift = true;
        }
        protected override bool StartScenarioInternal() {
            return ScenarioManager.Start<YharonGift>();
        }
    }
}

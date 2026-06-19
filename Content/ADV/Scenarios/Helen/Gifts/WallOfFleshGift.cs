using CalamityOverhaul.Content.ADV.ADVRewardPopups;
using CalamityOverhaul.Content.ADV.Common;
using CalamityOverhaul.Content.ADV.DialogueBoxs;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Helen.Gifts
{
    internal class WallOfFleshGift : GiftScenarioBase
    {
        public override string Key => nameof(WallOfFleshGift);
        public override int TargetBossID => NPCID.WallofFlesh;
        public static LocalizedText R1 { get; private set; }
        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }
        public static LocalizedText L4 { get; private set; }
        public override void SetStaticDefaults() {
            R1 = this.GetLocalization(nameof(R1), () => "比目鱼");
            L0 = this.GetLocalization(nameof(L0), () => "一堵由肉和骨组成的墙……这个世界的创造者一定有些特别的想法");
            L1 = this.GetLocalization(nameof(L1), () => "你刚才打破了某种平衡。感觉到了吗？世界的脉搏开始加速");
            L2 = this.GetLocalization(nameof(L2), () => "从那堆残骸里找到了这条饥饿鱼，它看起来永远吃不饱");
            L3 = this.GetLocalization(nameof(L3), () => "就像那堵墙一样，永远在追逐，永远在吞噬");
            L4 = this.GetLocalization(nameof(L4), () => "欢迎来到'困难模式'……虽然我觉得之前也不算简单");
        }
        protected override void Build() {
            DialogueBoxBase.RegisterPortrait(R1.Value, ADVAsset.HelenADV);
            DialogueBoxBase.RegisterPortrait(R1.Value + " ", ADVAsset.Helen_slightAnnoyedADV);
            DialogueBoxBase.RegisterPortrait(R1.Value + " " + " ", ADVAsset.Helen_solemnADV);
            Add(R1.Value + " ", L0.Value);
            Add(R1.Value + " " + " ", L1.Value);
            AddReward(R1.Value, L2.Value, ItemID.Hungerfish); //奖励
            Add(R1.Value, L3.Value);
            Add(R1.Value + " " + " ", L4.Value);
        }
        protected override bool IsGiftCompleted(ADVSave save) {
            return save.Get<BossGiftADVData>().WallOfFleshGift;
        }
        protected override void MarkGiftCompleted(ADVSave save) {
            save.Get<BossGiftADVData>().WallOfFleshGift = true;
        }
        protected override bool StartScenarioInternal() {
            return ScenarioManager.Start<WallOfFleshGift>();
        }
    }
}

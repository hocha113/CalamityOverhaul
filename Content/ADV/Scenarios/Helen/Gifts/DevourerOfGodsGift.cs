using CalamityOverhaul.Content.ADV.ADVRewardPopups;
using CalamityOverhaul.Content.ADV.Common;
using CalamityOverhaul.Content.ADV.DialogueBoxs;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Helen.Gifts
{
    internal class DevourerOfGodsGift : GiftScenarioBase
    {
        public override string Key => nameof(DevourerOfGodsGift);
        public override int TargetBossID => CWRID.NPC_DevourerofGodsHead;
        public static LocalizedText R1 { get; private set; }
        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }
        public static LocalizedText L4 { get; private set; }
        public override void SetStaticDefaults() {
            R1 = this.GetLocalization(nameof(R1), () => "比目鱼");
            L0 = this.GetLocalization(nameof(L0), () => "神明吞噬者……一条以神为食的宇宙之蛇。它的胃口和它的野心一样无限");
            L1 = this.GetLocalization(nameof(L1), () => "你知道吗？当你凝视深渊时，深渊也在凝视你。但这家伙不止凝视，它还想把你当零食");
            L2 = this.GetLocalization(nameof(L2), () => "霓虹四脚鱼，从虚空裂隙里飘出来的。它发出的光不属于这个维度");
            L3 = this.GetLocalization(nameof(L3), () => "看着它会让你的大脑尝试理解不该理解的颜色。这是一种……独特的体验");
            L4 = this.GetLocalization(nameof(L4), () => "如果你开始看到新的颜色，恭喜。如果你开始闻到颜色，那就该休息了");
        }
        protected override void Build() {
            DialogueBoxBase.RegisterPortrait(R1.Value, ADVAsset.HelenADV);
            DialogueBoxBase.RegisterPortrait(R1.Value + " ", ADVAsset.Helen_enjoyADV);
            Add(R1.Value, L0.Value);
            Add(R1.Value + " ", L1.Value);
            AddReward(R1.Value, L2.Value, ItemID.NeonTetra);//奖励
            Add(R1.Value, L3.Value);
            Add(R1.Value, L4.Value);
        }
        protected override bool IsGiftCompleted(ADVSave save) {
            return save.Get<BossGiftADVData>().DevourerOfGodsGift;
        }
        protected override void MarkGiftCompleted(ADVSave save) {
            save.Get<BossGiftADVData>().DevourerOfGodsGift = true;
        }
        protected override bool StartScenarioInternal() {
            return ScenarioManager.Start<DevourerOfGodsGift>();
        }
    }
}

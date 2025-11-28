using CalamityOverhaul.Content.ADV.ADVRewardPopups;
using CalamityOverhaul.Content.ADV.Common;
using CalamityOverhaul.Content.ADV.DialogueBoxs;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Helen.Gifts
{
    internal class EyeOfCthulhuGift : GiftScenarioBase
    {
        public override string Key => nameof(EyeOfCthulhuGift);
        public override int TargetBossID => NPCID.EyeofCthulhu;
        public static LocalizedText R1 { get; private set; }
        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }
        public static LocalizedText L4 { get; private set; }
        public override void SetStaticDefaults() {
            R1 = this.GetLocalization(nameof(R1), () => "比目鱼");
            L0 = this.GetLocalization(nameof(L0), () => "你的动作被那只巨眼拖成了慢镜头……我还以为你在刻意摆造型");
            L1 = this.GetLocalization(nameof(L1), () => "恭喜，你已经进入‘被注视的阶段’。这意味着……更多戏剧性的麻烦");
            L2 = this.GetLocalization(nameof(L2), () => "拿着，这是克苏鲁鱼。它和同名的神话一样，不太讲逻辑");
            L3 = this.GetLocalization(nameof(L3), () => "小心使用，它会让你误以为自己开眼了。其实那不过是血液里多了点兴奋剂");
            L4 = this.GetLocalization(nameof(L4), () => "我很好奇你会不会开始对月亮眨眼");
        }
        protected override void Build() {
            DialogueBoxBase.RegisterPortrait(R1.Value, ADVAsset.HelenADV);
            DialogueBoxBase.RegisterPortrait(R1.Value + " ", ADVAsset.Helen_naughtyADV);
            Add(R1.Value, L0.Value);
            Add(R1.Value, L1.Value);
            Add(R1.Value, L2.Value); //奖励
            Add(R1.Value, L3.Value);
            Add(R1.Value + " ", L4.Value);
        }
        public override void PreProcessSegment(DialogueBoxBase.DialoguePreProcessArgs args) {
            if (args.Index == 2) {
                ADVRewardPopup.ShowReward(ItemID.TheFishofCthulu, 1, null, appearDuration: 24, holdDuration: -1, giveDuration: 16, requireClick: true,
                    anchorProvider: () => {
                        var rect = DialogueUIRegistry.Current?.GetPanelRect() ?? Rectangle.Empty;
                        if (rect == Rectangle.Empty) {
                            return new Vector2(Main.screenWidth / 2f, Main.screenHeight * 0.45f);
                        }
                        return new Vector2(rect.Center.X, rect.Y - 70f);
                    }, offset: Vector2.Zero);
            }
        }
        protected override bool IsGiftCompleted(ADVSave save) {
            return save.EyeOfCthulhuGift;
        }
        protected override void MarkGiftCompleted(ADVSave save) {
            save.EyeOfCthulhuGift = true;
        }
        protected override bool StartScenarioInternal() {
            return ScenarioManager.Start<EyeOfCthulhuGift>();
        }
    }
}

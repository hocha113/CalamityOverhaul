using CalamityOverhaul.Content.ADV.ADVRewardPopups;
using CalamityOverhaul.Content.ADV.Common;
using CalamityOverhaul.Content.ADV.DialogueBoxs;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Helen.Gifts
{
    internal class PlaguebringerGift : GiftScenarioBase
    {
        public override string Key => nameof(PlaguebringerGift);
        public override int TargetBossID => CWRID.NPC_PlaguebringerGoliath;
        public static LocalizedText R1 { get; private set; }
        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }
        public static LocalizedText L4 { get; private set; }
        public override void SetStaticDefaults() {
            R1 = this.GetLocalization(nameof(R1), () => "比目鱼");
            L0 = this.GetLocalization(nameof(L0), () => "瘟疫的使者……一只机械蜜蜂携带着生物武器。谁设计的这个？一定是个混蛋，竟敢奴役蜜蜂");
            L1 = this.GetLocalization(nameof(L1), () => "病毒不在乎对错，它只是想活下去。就像我们一样，只是手段更直接");
            L2 = this.GetLocalization(nameof(L2), () => "紫晶鱼，被感染过又奇迹般痊愈的。它的免疫系统比你的人生经历还丰富");
            L3 = this.GetLocalization(nameof(L3), () => "据说吃了它能增强抵抗力。但我觉得这更像是'杀不死你的让你更强'的另一种说法");
            L4 = this.GetLocalization(nameof(L4), () => "不过既然你刚从瘟疫蜂窝里走出来，这点小风险应该不算什么");
        }
        protected override void Build() {
            DialogueBoxBase.RegisterPortrait(R1.Value, ADVAsset.HelenADV);
            DialogueBoxBase.RegisterPortrait(R1.Value + " ", ADVAsset.Helen_slightAnnoyedADV);
            Add(R1.Value + " ", L0.Value);
            Add(R1.Value, L1.Value);
            Add(R1.Value, L2.Value); //奖励
            Add(R1.Value, L3.Value);
            Add(R1.Value, L4.Value);
        }
        public override void PreProcessSegment(DialogueBoxBase.DialoguePreProcessArgs args) {
            if (args.Index == 2) {
                ADVRewardPopup.ShowReward(ItemID.Jewelfish, 1, null, appearDuration: 24, holdDuration: -1, giveDuration: 16, requireClick: true,
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
            return save.PlaguebringerGift;
        }
        protected override void MarkGiftCompleted(ADVSave save) {
            save.PlaguebringerGift = true;
        }
        protected override bool StartScenarioInternal() {
            return ScenarioManager.Start<PlaguebringerGift>();
        }
    }
}

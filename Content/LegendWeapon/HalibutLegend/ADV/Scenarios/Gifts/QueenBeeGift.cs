using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.ADV.Scenarios.Gifts
{
    internal class QueenBeeGift : GiftScenarioBase
    {
        public override string Key => nameof(QueenBeeGift);
        public override int TargetBossID => NPCID.QueenBee;
        public static LocalizedText R1 { get; private set; }
        public static LocalizedText R2 { get; private set; }
        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }
        public override void SetStaticDefaults() {
            R1 = this.GetLocalization(nameof(R1), () => "比目鱼");
            R2 = this.GetLocalization(nameof(R2), () => "???");
            L0 = this.GetLocalization(nameof(L0), () => "我差点以为脸要被埋进巢里舀一口");
            L1 = this.GetLocalization(nameof(L1), () => "不过，我刚才从地上堆积的蜂蜜里摸到了一条鱼");
            L2 = this.GetLocalization(nameof(L2), () => "给，新鲜还热乎的蜂蜜鱼");
            L3 = this.GetLocalization(nameof(L3), () => "我觉得它非常适合做糖醋鲤鱼");
        }
        protected override void Build() {
            DialogueBoxBase.RegisterPortrait(R1.Value, ADVAsset.HelenADV);
            DialogueBoxBase.SetPortraitStyle(R1.Value, silhouette: false);
            Add(R1.Value, L0.Value);
            Add(R1.Value, L1.Value);
            Add(R1.Value, L2.Value); //奖励
            Add(R1.Value, L3.Value);
        }
        public override void PreProcessSegment(DialogueBoxBase.DialoguePreProcessArgs args) {
            if (args.Index == 2) {
                ADVRewardPopup.ShowReward(ItemID.Honeyfin, 1, null, appearDuration: 24, holdDuration: -1, giveDuration: 16, requireClick: true,
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
            return save.QueenBeeGift;
        }
        protected override void MarkGiftCompleted(ADVSave save) {
            save.QueenBeeGift = true;
        }
        protected override bool StartScenarioInternal() {
            return ScenarioManager.Start<QueenBeeGift>();
        }
    }
}

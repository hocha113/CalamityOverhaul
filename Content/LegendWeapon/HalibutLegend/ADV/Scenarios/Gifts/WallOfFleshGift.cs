using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.ADV.Scenarios.Gifts
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
            DialogueBoxBase.SetPortraitStyle(R1.Value, silhouette: false);
            Add(R1.Value, L0.Value);
            Add(R1.Value, L1.Value);
            Add(R1.Value, L2.Value); //奖励
            Add(R1.Value, L3.Value);
            Add(R1.Value, L4.Value);
        }
        public override void PreProcessSegment(DialogueBoxBase.DialoguePreProcessArgs args) {
            if (args.Index == 2) {
                ADVRewardPopup.ShowReward(ItemID.Hungerfish, 1, null, appearDuration: 24, holdDuration: -1, giveDuration: 16, requireClick: true,
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
            return save.WallOfFleshGift;
        }
        protected override void MarkGiftCompleted(ADVSave save) {
            save.WallOfFleshGift = true;
        }
        protected override bool StartScenarioInternal() {
            return ScenarioManager.Start<WallOfFleshGift>();
        }
    }
}

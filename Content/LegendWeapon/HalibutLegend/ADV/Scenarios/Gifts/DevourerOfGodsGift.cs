using CalamityMod.NPCs.DevourerofGods;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.ADV.Scenarios.Gifts
{
    internal class DevourerOfGodsGift : GiftScenarioBase
    {
        public override string Key => nameof(DevourerOfGodsGift);
        public override int TargetBossID => ModContent.NPCType<DevourerofGodsHead>();
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
            DialogueBoxBase.SetPortraitStyle(R1.Value, silhouette: false);
            Add(R1.Value, L0.Value);
            Add(R1.Value, L1.Value);
            Add(R1.Value, L2.Value);//奖励
            Add(R1.Value, L3.Value);
            Add(R1.Value, L4.Value);
        }
        public override void PreProcessSegment(DialogueBoxBase.DialoguePreProcessArgs args) {
            if (args.Index == 2) {
                ADVRewardPopup.ShowReward(ItemID.NeonTetra, 1, null, appearDuration: 24, holdDuration: -1, giveDuration: 16, requireClick: true,
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
            return save.DevourerOfGodsGift;
        }
        protected override void MarkGiftCompleted(ADVSave save) {
            save.DevourerOfGodsGift = true;
        }
        protected override bool StartScenarioInternal() {
            return ScenarioManager.Start<DevourerOfGodsGift>();
        }
    }
}

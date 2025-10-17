using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.ADV.Scenarios.Gifts
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
            L1 = this.GetLocalization(nameof(L1), () => "它守护着什么吗？还是只是在重复一个早已失去意义的程序？");
            L2 = this.GetLocalization(nameof(L2), () => "岩鱼，从神殿的地基里挖出来的。它的密度高到让我怀疑重力是不是坏了");
            L3 = this.GetLocalization(nameof(L3), () => "我以前试着煮过它，但我的锅先投降了");
            L4 = this.GetLocalization(nameof(L4), () => "......有些东西存在的意义就是让人意识到，并非所有问题都需要被解决");
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
                ADVRewardPopup.ShowReward(ItemID.Rockfish, 1, null, appearDuration: 24, holdDuration: -1, giveDuration: 16, requireClick: true,
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
            return save.GolemGift;
        }
        protected override void MarkGiftCompleted(ADVSave save) {
            save.GolemGift = true;
        }
        protected override bool StartScenarioInternal() {
            return ScenarioManager.Start<GolemGift>();
        }
    }
}

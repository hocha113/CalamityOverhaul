using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.ADV.Scenarios.Gifts
{
    internal class SlimeGodGift : ADVScenarioBase, ILocalizedModType
    {
        public override string Key => nameof(SlimeGodGift);
        public string LocalizationCategory => "Legend.HalibutText.ADV";
        public static LocalizedText R1 { get; private set; }
        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }
        public static LocalizedText L4 { get; private set; }
        public override void SetStaticDefaults() {
            R1 = this.GetLocalization(nameof(R1), () => "比目鱼");
            L0 = this.GetLocalization(nameof(L0), () => "三个史莱姆，呃，或者说一个分裂的神性？这种二元对立的存在形式很有意思");
            L1 = this.GetLocalization(nameof(L1), () => "腐化与猩红，就像这个世界永恒的矛盾。而你刚才证明了矛盾可以被'解决'");
            L2 = this.GetLocalization(nameof(L2), () => "这是杂色猪油鱼，从那堆粘液里捞出来的。别问我为什么叫这个名字");
            L3 = this.GetLocalization(nameof(L3), () => "它的颜色会随着观察者的心情改变，或者说，它在模仿你内心的混乱");
            L4 = this.GetLocalization(nameof(L4), () => "如果你盯着它看太久，可能会开始思考自己到底属于哪一边");
        }
        protected override void Build() {
            DialogueBoxBase.RegisterPortrait(R1.Value, ADVAsset.HeadADV);
            DialogueBoxBase.SetPortraitStyle(R1.Value, silhouette: false);
            Add(R1.Value, L0.Value);
            Add(R1.Value, L1.Value);
            Add(R1.Value, L2.Value); //奖励
            Add(R1.Value, L3.Value);
            Add(R1.Value, L4.Value);
        }
        public override void PreProcessSegment(DialogueBoxBase.DialoguePreProcessArgs args) {
            if (args.Index == 2) {
                ADVRewardPopup.ShowReward(ItemID.VariegatedLardfish, 1, null, appearDuration: 24, holdDuration: -1, giveDuration: 16, requireClick: true,
                    anchorProvider: () => {
                        var rect = DialogueUIRegistry.Current?.GetPanelRect() ?? Rectangle.Empty;
                        if (rect == Rectangle.Empty) {
                            return new Vector2(Main.screenWidth / 2f, Main.screenHeight * 0.45f);
                        }
                        return new Vector2(rect.Center.X, rect.Y - 70f);
                    }, offset: Vector2.Zero);
            }
        }
        public override void Update(ADVSave save, HalibutPlayer halibutPlayer) {
            if (!halibutPlayer.HeldHalibut) {
                return;
            }
            if (!save.FirstMet) {
                return;//必须先触发过初次见面
            }
            if (save.SlimeGodGift) {
                return;
            }
            if (!InWorldBossPhase.Downed5.Invoke()) {
                return;
            }

            if (ScenarioManager.Start<SlimeGodGift>()) {
                save.SlimeGodGift = true;
            }
        }
    }
}

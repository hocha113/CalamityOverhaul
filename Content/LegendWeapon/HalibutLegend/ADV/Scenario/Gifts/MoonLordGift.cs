using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.ADV.Scenario.Gifts
{
    internal class MoonLordGift : ADVScenarioBase, ILocalizedModType
    {
        public override string Key => nameof(MoonLordGift);
        public string LocalizationCategory => "Legend.HalibutText.ADV";
        public static LocalizedText R1 { get; private set; }
        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }
        public static LocalizedText L4 { get; private set; }
        public override void SetStaticDefaults() {
            R1 = this.GetLocalization(nameof(R1), () => "比目鱼");
            L0 = this.GetLocalization(nameof(L0), () => "月球的主宰，哈！或者说，曾经是。现在它只是一堆漂浮的眼球和血肉");
            L1 = this.GetLocalization(nameof(L1), () => "我们刚才不仅拯救了世界，还关闭了一个从未被正确打开的门");
            L2 = this.GetLocalization(nameof(L2), () => "云鱼，从天空的裂缝里飘下来的。它在手里像是没有重量");
            L3 = this.GetLocalization(nameof(L3), () => "据说它能让人看到未来……但那个未来已经被你改写了");
            L4 = this.GetLocalization(nameof(L4), () => "我们刚刚证明了，即使是神，也会流血");
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
                ADVRewardPopup.ShowReward(ItemID.Cloudfish, 1, null, appearDuration: 24, holdDuration: -1, giveDuration: 16, requireClick: true,
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
            if (save.MoonLordGift) {
                return;
            }
            if (!InWorldBossPhase.VDownedV16.Invoke()) {
                return;
            }

            if (ScenarioManager.Start<MoonLordGift>()) {
                save.MoonLordGift = true;
            }
        }
    }
}

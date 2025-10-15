using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.ADV.Scenario.Gifts
{
    internal class CryogenGift : ADVScenarioBase, ILocalizedModType
    {
        public override string Key => nameof(CryogenGift);
        public string LocalizationCategory => "Legend.HalibutText.ADV";
        public static LocalizedText R1 { get; private set; }
        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }
        public static LocalizedText L4 { get; private set; }
        public override void SetStaticDefaults() {
            R1 = this.GetLocalization(nameof(R1), () => "比目鱼");
            L0 = this.GetLocalization(nameof(L0), () => "一团被冰冻的灵魂......温度低到连时间都想躺下来休息");
            L1 = this.GetLocalization(nameof(L1), () => "你知道吗？寒冷的尽头并非死亡，是一种永恒的静止");
            L2 = this.GetLocalization(nameof(L2), () => "霜鲦鱼，从冰晶碎片里解冻出来的。它还保持着被冻结时的姿态");
            L3 = this.GetLocalization(nameof(L3), () => "据说吃了它会让你的血液降温，思维变得异常清醒");
            L4 = this.GetLocalization(nameof(L4), () => "不过别多吃，除非你真的想成为一个冰雕");
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
                ADVRewardPopup.ShowReward(ItemID.FrostMinnow, 1, null, appearDuration: 24, holdDuration: -1, giveDuration: 16, requireClick: true,
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
            if (save.CryogenGift) {
                return;
            }
            if (!InWorldBossPhase.Downed6.Invoke()) {
                return;
            }

            if (ScenarioManager.Start<CryogenGift>()) {
                save.CryogenGift = true;
            }
        }
    }
}

using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.ADV.Scenario
{
    internal class CalamitasCloneGift : ADVScenarioBase, ILocalizedModType
    {
        public override string Key => nameof(CalamitasCloneGift);
        public string LocalizationCategory => "Legend.HalibutText.ADV";
        public static LocalizedText R1 { get; private set; }
        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }
        public static LocalizedText L4 { get; private set; }
        public override void SetStaticDefaults() {
            R1 = this.GetLocalization(nameof(R1), () => "比目鱼");
            L0 = this.GetLocalization(nameof(L0), () => "一个影子……但影子不该有自己的意志。除非原型已经强大到开始分裂");
            L1 = this.GetLocalization(nameof(L1), () => "你击败的不是她，而是她的回声。真正的恐怖还在更深的地方等着");
            L2 = this.GetLocalization(nameof(L2), () => "恶魔鱼，从灾厄的余烬中提取的。它在你手里低语着不该被听到的秘密");
            L3 = this.GetLocalization(nameof(L3), () => "如果你开始听懂它在说什么......恭喜，你已经迈出了疯狂的第一步");
            L4 = this.GetLocalization(nameof(L4), () => "不过别担心，疯狂也是一种清醒，只是角度不同而已");
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
                ADVRewardPopup.ShowReward(ItemID.DemonicHellfish, 1, null, appearDuration: 24, holdDuration: -1, giveDuration: 16, requireClick: true,
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
            if (save.CalamitasCloneGift) {
                return;
            }
            if (!InWorldBossPhase.Downed10.Invoke()) {
                return;
            }

            if (ScenarioManager.Start<CalamitasCloneGift>()) {
                save.CalamitasCloneGift = true;
            }
        }
    }
}

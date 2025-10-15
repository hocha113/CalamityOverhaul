using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.ADV.Scenario
{
    internal class SkeletronGift : ADVScenarioBase, ILocalizedModType
    {
        public override string Key => nameof(SkeletronGift);
        public string LocalizationCategory => "Legend.HalibutText.ADV";
        public static LocalizedText R1 { get; private set; }
        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }
        public static LocalizedText L4 { get; private set; }
        public override void SetStaticDefaults() {
            R1 = this.GetLocalization(nameof(R1), () => "比目鱼");
            L0 = this.GetLocalization(nameof(L0), () => "那可真是一大堆钙质!");
            L1 = this.GetLocalization(nameof(L1), () => "那东西的颅骨结构，让我想起一只失控的意念聚合体");
            L2 = this.GetLocalization(nameof(L2), () => "让我枪管冷却一下，我刚才从这周围捡到了一条鱼");
            L3 = this.GetLocalization(nameof(L3), () => "你看，这是‘骷髅王鱼’，据说它体内的磷质能让夜钓的人思考人生");
            L4 = this.GetLocalization(nameof(L4), () => "走吧，前面还有更抽象的骨头在等着你");
        }
        protected override void Build() {
            DialogueBoxBase.RegisterPortrait(R1.Value, ADVAsset.HeadADV);
            DialogueBoxBase.SetPortraitStyle(R1.Value, silhouette: false);
            Add(R1.Value, L0.Value);
            Add(R1.Value, L1.Value);
            Add(R1.Value, L2.Value);
            Add(R1.Value, L3.Value); //奖励
            Add(R1.Value, L4.Value);
        }
        public override void PreProcessSegment(DialogueBoxBase.DialoguePreProcessArgs args) {
            if (args.Index == 3) {
                ADVRewardPopup.ShowReward(ItemID.Fishotron, 1, null, appearDuration: 24, holdDuration: -1, giveDuration: 16, requireClick: true,
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
            if (save.SkeletronGift) {
                return;
            }
            if (!NPC.downedBoss3) {
                return;
            }

            if (ScenarioManager.Start<SkeletronGift>()) {
                save.SkeletronGift = true;
            }
        }
    }
}

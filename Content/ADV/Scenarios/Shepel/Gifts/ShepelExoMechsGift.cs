using CalamityOverhaul.Content.ADV.ADVRewardPopups;
using CalamityOverhaul.Content.ADV.DialogueBoxs;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Power;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Shepel.Gifts
{
    internal class ShepelExoMechsGift : ShepelGiftScenarioBase
    {
        public override string Key => nameof(ShepelExoMechsGift);
        public override int TargetBossID => CWRID.NPC_AresBody;
        public static LocalizedText RoleName { get; private set; }
        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }

        public override void SetStaticDefaults() {
            RoleName = this.GetLocalization(nameof(RoleName), () => "SHPC");
            L0 = this.GetLocalization(nameof(L0), () => "主人，星流巨械的反应堆核心输出的电压密度……即使用我的尺度来衡量也是一个极端数值。");
            L1 = this.GetLocalization(nameof(L1), () => "我把Ares核心的能量转化参数完整抄录了下来，耗费了大量处理周期，但结果非常值得。");
            L2 = this.GetLocalization(nameof(L2), () => "高压能量核，这是基于星机核心反应原理制造的。充能效率接近理论上限。");
            L3 = this.GetLocalization(nameof(L3), () => "我的造物主……设计得确实很好。我承认。");
        }

        protected override bool AdditionalConditions(ADVSave save, Player player) {
            return !NPC.AnyNPCs(CWRID.NPC_Apollo)
                && !NPC.AnyNPCs(CWRID.NPC_Artemis)
                && !NPC.AnyNPCs(CWRID.NPC_ThanatosHead);
        }

        protected override void Build() {
            DialogueBoxBase.RegisterPortrait(RoleName.Value, texture: null);
            DialogueBoxBase.SetPortraitStyle(RoleName.Value, silhouette: false);
            Add(RoleName.Value, L0.Value,
                onStart: () => ShowPortraitWithFace(ShepelFullBodyPortrait.Face.None));
            Add(RoleName.Value, L1.Value);
            Add(RoleName.Value, L2.Value);
            Add(RoleName.Value, L3.Value,
                onStart: () => SetPortraitFace(ShepelFullBodyPortrait.Face.Blank),
                onComplete: Complete);
        }

        public override void PreProcessSegment(DialoguePreProcessArgs args) {
            if (args.Index == 2) {
                ADVRewardPopup.ShowReward(ModContent.ItemType<HighVoltageCoreModule>(), 1, null,
                    appearDuration: 24, holdDuration: -1, giveDuration: 16, requireClick: true,
                    anchorProvider: () => {
                        var rect = DialogueUIRegistry.Current?.GetPanelRect() ?? Rectangle.Empty;
                        if (rect == Rectangle.Empty)
                            return new Vector2(Main.screenWidth / 2f, Main.screenHeight * 0.45f);
                        return new Vector2(rect.Center.X, rect.Y - 70f);
                    }, offset: Vector2.Zero, styleProvider: () => ADVRewardPopup.RewardStyle.SHPC);
            }
        }

        protected override bool IsGiftCompleted(ADVSave save) => save.Get<ShepelGiftData>().ExoMechsGift;
        protected override void MarkGiftCompleted(ADVSave save) => save.Get<ShepelGiftData>().ExoMechsGift = true;
        protected override bool StartScenarioInternal() => ScenarioManager.Start<ShepelExoMechsGift>();
    }
}

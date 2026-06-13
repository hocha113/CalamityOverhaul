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
            L0 = this.GetLocalization(nameof(L0), () => "结束了。那个被称为源头的存在，终于也倒在您的火力之下了。");
            L1 = this.GetLocalization(nameof(L1), () => "主人，您做到了。我的系统日志正在全速记录这一刻的数据，散热模块甚至有些超负荷了。");
            L2 = this.GetLocalization(nameof(L2), () => "我提取了星流泰坦的核心数据为您升级。现在，我是比它更高效的兵器，也是专属于您的造物。");
            L3 = this.GetLocalization(nameof(L3), () => "证明了这一点，我的存在才更有价值。今后也请继续使用我吧。");
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

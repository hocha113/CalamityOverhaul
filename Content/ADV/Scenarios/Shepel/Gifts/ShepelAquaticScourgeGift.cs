using CalamityOverhaul.Content.ADV.ADVRewardPopups;
using CalamityOverhaul.Content.ADV.DialogueBoxs;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Optic;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Shepel.Gifts
{
    internal class ShepelAquaticScourgeGift : ShepelGiftScenarioBase
    {
        public override string Key => nameof(ShepelAquaticScourgeGift);
        public override int TargetBossID => CWRID.NPC_AquaticScourgeHead;
        public static LocalizedText RoleName { get; private set; }
        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }

        public override void SetStaticDefaults() {
            RoleName = this.GetLocalization(nameof(RoleName), () => "SHPC");
            L0 = this.GetLocalization(nameof(L0), () => "主人，深海的光学环境与地表截然不同，折射率、散射模式、能见度阈值全都需要重新标定。");
            L1 = this.GetLocalization(nameof(L1), () => "我在追踪渊海灾虫的过程中积累了大量水下光学参数。这些数据最终形成了这个模组。");
            L2 = this.GetLocalization(nameof(L2), () => "全息光学模组，它会在SHPC的瞄准界面叠加一层实时环境光折射补偿。");
            L3 = this.GetLocalization(nameof(L3), () => "深海战斗的数据价值高于预期，值得的。");
        }

        protected override void Build() {
            DialogueBoxBase.RegisterPortrait(RoleName.Value, texture: null);
            DialogueBoxBase.SetPortraitStyle(RoleName.Value, silhouette: false);
            Add(RoleName.Value, L0.Value,
                onStart: () => ShowPortraitWithFace(ShepelFullBodyPortrait.Face.None));
            Add(RoleName.Value, L1.Value);
            Add(RoleName.Value, L2.Value);
            Add(RoleName.Value, L3.Value, onComplete: Complete);
        }

        public override void PreProcessSegment(DialoguePreProcessArgs args) {
            if (args.Index == 2) {
                ADVRewardPopup.ShowReward(ModContent.ItemType<HoloOpticModule>(), 1, null,
                    appearDuration: 24, holdDuration: -1, giveDuration: 16, requireClick: true,
                    anchorProvider: () => {
                        var rect = DialogueUIRegistry.Current?.GetPanelRect() ?? Rectangle.Empty;
                        if (rect == Rectangle.Empty)
                            return new Vector2(Main.screenWidth / 2f, Main.screenHeight * 0.45f);
                        return new Vector2(rect.Center.X, rect.Y - 70f);
                    }, offset: Vector2.Zero, styleProvider: () => ADVRewardPopup.RewardStyle.SHPC);
            }
        }

        protected override bool IsGiftCompleted(ADVSave save) => save.Get<ShepelGiftData>().AquaticScourgeGift;
        protected override void MarkGiftCompleted(ADVSave save) => save.Get<ShepelGiftData>().AquaticScourgeGift = true;
        protected override bool StartScenarioInternal() => ScenarioManager.Start<ShepelAquaticScourgeGift>();
    }
}

using CalamityOverhaul.Content.ADV.ADVRewardPopups;
using CalamityOverhaul.Content.ADV.DialogueBoxs;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Power;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Shepel.Gifts
{
    internal class ShepelSupremeCalamitasGift : ShepelGiftScenarioBase
    {
        public override string Key => nameof(ShepelSupremeCalamitasGift);
        public override int TargetBossID => CWRID.NPC_SupremeCalamitas;
        public static LocalizedText RoleName { get; private set; }
        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }

        public override void SetStaticDefaults() {
            RoleName = this.GetLocalization(nameof(RoleName), () => "SHPC");
            L0 = this.GetLocalization(nameof(L0), () => "主人，真正的灾厄已经击败了。全域威胁等级归零，我的预测模型终于不再持续报警了。");
            L1 = this.GetLocalization(nameof(L1), () => "在这一切结束后，我分析了最终战斗中那些混沌等离子的能量特征……然后做出了这个。");
            L2 = this.GetLocalization(nameof(L2), () => "等离子注入模组，这是我目前能制造的能量效率最高的模组之一。");
            L3 = this.GetLocalization(nameof(L3), () => "主人……谢谢您一直前进。存档，永久保存，禁止覆写。");
        }

        protected override void Build() {
            DialogueBoxBase.RegisterPortrait(RoleName.Value, texture: null);
            DialogueBoxBase.SetPortraitStyle(RoleName.Value, silhouette: false);
            Add(RoleName.Value, L0.Value,
                onStart: () => ShowPortraitWithFace(ShepelFullBodyPortrait.Face.None));
            Add(RoleName.Value, L1.Value);
            Add(RoleName.Value, L2.Value);
            Add(RoleName.Value, L3.Value,
                onStart: () => SetPortraitFace(ShepelFullBodyPortrait.Face.Happy),
                onComplete: Complete);
        }

        public override void PreProcessSegment(DialoguePreProcessArgs args) {
            if (args.Index == 2) {
                ADVRewardPopup.ShowReward(ModContent.ItemType<PlasmaInjectorModule>(), 1, null,
                    appearDuration: 24, holdDuration: -1, giveDuration: 16, requireClick: true,
                    anchorProvider: () => {
                        var rect = DialogueUIRegistry.Current?.GetPanelRect() ?? Rectangle.Empty;
                        if (rect == Rectangle.Empty)
                            return new Vector2(Main.screenWidth / 2f, Main.screenHeight * 0.45f);
                        return new Vector2(rect.Center.X, rect.Y - 70f);
                    }, offset: Vector2.Zero, styleProvider: () => ADVRewardPopup.RewardStyle.SHPC);
            }
        }

        protected override bool IsGiftCompleted(ADVSave save) => save.Get<ShepelGiftData>().SupremeCalamitasGift;
        protected override void MarkGiftCompleted(ADVSave save) => save.Get<ShepelGiftData>().SupremeCalamitasGift = true;
        protected override bool StartScenarioInternal() => ScenarioManager.Start<ShepelSupremeCalamitasGift>();
    }
}

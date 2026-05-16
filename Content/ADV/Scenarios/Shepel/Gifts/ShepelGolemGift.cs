using CalamityOverhaul.Content.ADV.ADVRewardPopups;
using CalamityOverhaul.Content.ADV.DialogueBoxs;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Stock;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Shepel.Gifts
{
    internal class ShepelGolemGift : ShepelGiftScenarioBase
    {
        public override string Key => nameof(ShepelGolemGift);
        public override int TargetBossID => NPCID.Golem;
        public static LocalizedText RoleName { get; private set; }
        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }

        public override void SetStaticDefaults() {
            RoleName = this.GetLocalization(nameof(RoleName), () => "SHPC");
            L0 = this.GetLocalization(nameof(L0), () => "主人，石巨人的物理冲击数据相当可观——高质量、高频率、低精度，标准的暴力覆盖策略。");
            L1 = this.GetLocalization(nameof(L1), () => "我把那些冲击波形数据做成了一套动能阻尼参数，实际上是在用它的重击对抗它自己。");
            L2 = this.GetLocalization(nameof(L2), () => "动能阻尼模组，这个模组降低了受击后的姿态扰动，主人的射击窗口会更稳定。");
            L3 = this.GetLocalization(nameof(L3), () => "巨型机器人的存在意义之一就是提供充足的冲击波形样本。");
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
                ADVRewardPopup.ShowReward(ModContent.ItemType<KineticDamperModule>(), 1, null,
                    appearDuration: 24, holdDuration: -1, giveDuration: 16, requireClick: true,
                    anchorProvider: () => {
                        var rect = DialogueUIRegistry.Current?.GetPanelRect() ?? Rectangle.Empty;
                        if (rect == Rectangle.Empty)
                            return new Vector2(Main.screenWidth / 2f, Main.screenHeight * 0.45f);
                        return new Vector2(rect.Center.X, rect.Y - 70f);
                    }, offset: Vector2.Zero, styleProvider: () => ADVRewardPopup.RewardStyle.SHPC);
            }
        }

        protected override bool IsGiftCompleted(ADVSave save) => save.Get<ShepelGiftData>().GolemGift;
        protected override void MarkGiftCompleted(ADVSave save) => save.Get<ShepelGiftData>().GolemGift = true;
        protected override bool StartScenarioInternal() => ScenarioManager.Start<ShepelGolemGift>();
    }
}

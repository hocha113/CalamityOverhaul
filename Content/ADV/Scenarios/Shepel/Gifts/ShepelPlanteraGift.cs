using CalamityOverhaul.Content.ADV.ADVRewardPopups;
using CalamityOverhaul.Content.ADV.DialogueBoxs;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Optic;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Shepel.Gifts
{
    internal class ShepelPlanteraGift : ShepelGiftScenarioBase
    {
        public override string Key => nameof(ShepelPlanteraGift);
        public override int TargetBossID => NPCID.Plantera;
        public static LocalizedText RoleName { get; private set; }
        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }

        public override void SetStaticDefaults() {
            RoleName = this.GetLocalization(nameof(RoleName), () => "SHPC");
            L0 = this.GetLocalization(nameof(L0), () => "地下丛林里的藤蔓长得太放肆了，到处都是恼人的倒刺和孢子。");
            L1 = this.GetLocalization(nameof(L1), () => "那朵食人花伪装得很巧妙，但也不过是一堆等待清理的杂草而已。");
            L2 = this.GetLocalization(nameof(L2), () => "我改进了瞄具的动态视觉。这下子，那些挡视线的阔叶就再也无法干扰您的判断了。");
            L3 = this.GetLocalization(nameof(L3), () => "偶尔兼职一下园丁，为您扫清花园里的路障，也是很有趣的工作呢。");
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
                ADVRewardPopup.ShowReward(ModContent.ItemType<AdaptiveOpticModule>(), 1, null,
                    appearDuration: 24, holdDuration: -1, giveDuration: 16, requireClick: true,
                    anchorProvider: () => {
                        var rect = DialogueUIRegistry.Current?.GetPanelRect() ?? Rectangle.Empty;
                        if (rect == Rectangle.Empty)
                            return new Vector2(Main.screenWidth / 2f, Main.screenHeight * 0.45f);
                        return new Vector2(rect.Center.X, rect.Y - 70f);
                    }, offset: Vector2.Zero, styleProvider: () => ADVRewardPopup.RewardStyle.SHPC);
            }
        }

        protected override bool IsGiftCompleted(ADVSave save) => save.Get<ShepelGiftData>().PlanteraGift;
        protected override void MarkGiftCompleted(ADVSave save) => save.Get<ShepelGiftData>().PlanteraGift = true;
        protected override bool StartScenarioInternal() => ScenarioManager.Start<ShepelPlanteraGift>();
    }
}

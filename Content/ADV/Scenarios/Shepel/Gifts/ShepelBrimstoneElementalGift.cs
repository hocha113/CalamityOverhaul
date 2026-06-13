using CalamityOverhaul.Content.ADV.ADVRewardPopups;
using CalamityOverhaul.Content.ADV.DialogueBoxs;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Barrel;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Shepel.Gifts
{
    internal class ShepelBrimstoneElementalGift : ShepelGiftScenarioBase
    {
        public override string Key => nameof(ShepelBrimstoneElementalGift);
        public override int TargetBossID => CWRID.NPC_BrimstoneElemental;
        public static LocalizedText RoleName { get; private set; }
        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }

        public override void SetStaticDefaults() {
            RoleName = this.GetLocalization(nameof(RoleName), () => "SHPC");
            L0 = this.GetLocalization(nameof(L0), () => "呼，那种快要把人烤化的高温终于降下来了。这个女士的脾气可真是火爆。");
            L1 = this.GetLocalization(nameof(L1), () => "让我仔细看看……万幸，您的衣角连一丝被烧焦的痕迹都没有。");
            L2 = this.GetLocalization(nameof(L2), () => "趁着刚才收集到的受热数据，我给您的武器做了一次特别的热处理。");
            L3 = this.GetLocalization(nameof(L3), () => "换上这根全新的枪管吧！下次再遇到粗鲁的家伙，请毫无顾忌地开火，所有的散热工作都包在我身上。");
        }

        protected override void Build() {
            DialogueBoxBase.RegisterPortrait(RoleName.Value, texture: null);
            DialogueBoxBase.SetPortraitStyle(RoleName.Value, silhouette: false);
            Add(RoleName.Value, L0.Value,
                onStart: () => ShowPortraitWithFace(ShepelFullBodyPortrait.Face.None));
            Add(RoleName.Value, L1.Value);
            Add(RoleName.Value, L2.Value);
            Add(RoleName.Value, L3.Value,
                onStart: () => SetPortraitFace(ShepelFullBodyPortrait.Face.Smirk),
                onComplete: Complete);
        }

        public override void PreProcessSegment(DialoguePreProcessArgs args) {
            if (args.Index == 2) {
                ADVRewardPopup.ShowReward(ModContent.ItemType<MagmaVentBarrelModule>(), 1, null,
                    appearDuration: 24, holdDuration: -1, giveDuration: 16, requireClick: true,
                    anchorProvider: () => {
                        var rect = DialogueUIRegistry.Current?.GetPanelRect() ?? Rectangle.Empty;
                        if (rect == Rectangle.Empty)
                            return new Vector2(Main.screenWidth / 2f, Main.screenHeight * 0.45f);
                        return new Vector2(rect.Center.X, rect.Y - 70f);
                    }, offset: Vector2.Zero, styleProvider: () => ADVRewardPopup.RewardStyle.SHPC);
            }
        }

        protected override bool IsGiftCompleted(ADVSave save) => save.Get<ShepelGiftData>().BrimstoneElementalGift;
        protected override void MarkGiftCompleted(ADVSave save) => save.Get<ShepelGiftData>().BrimstoneElementalGift = true;
        protected override bool StartScenarioInternal() => ScenarioManager.Start<ShepelBrimstoneElementalGift>();
    }
}

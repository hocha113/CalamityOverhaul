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
            L0 = this.GetLocalization(nameof(L0), () => "主人，硫磺火元素的核心温度超出了我所有传感器的量程上限。");
            L1 = this.GetLocalization(nameof(L1), () => "我不得不用间接推算的方式重建了它的热量分布模型，然后把这份数据烧进了一个枪管参数包里。");
            L2 = this.GetLocalization(nameof(L2), () => "焦化枪管模组。光束在命中时会附带额外的热能爆发效果。");
            L3 = this.GetLocalization(nameof(L3), () => "高热研究容易让人上头，但我控制住了。");
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

using CalamityOverhaul.Content.ADV.ADVRewardPopups;
using CalamityOverhaul.Content.ADV.DialogueBoxs;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Stock;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Shepel.Gifts
{
    internal class ShepelSkeletronPrimeGift : ShepelGiftScenarioBase
    {
        public override string Key => nameof(ShepelSkeletronPrimeGift);
        public override int TargetBossID => NPCID.SkeletronPrime;
        public static LocalizedText RoleName { get; private set; }
        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }

        public override void SetStaticDefaults() {
            RoleName = this.GetLocalization(nameof(RoleName), () => "SHPC");
            L0 = this.GetLocalization(nameof(L0), () => "主人，机械骷髅王在多武装切换时的射频节奏我研究了很久——快速部署、即时火力转换。");
            L1 = this.GetLocalization(nameof(L1), () => "这种攻击节奏的数学模型比我预想的复杂。最终我把它简化成了一个枪托配置包。");
            L2 = this.GetLocalization(nameof(L2), () => "突袭枪托模组，攻击速度和快速交战表现都有改善。");
            L3 = this.GetLocalization(nameof(L3), () => "旧王的骷髅身上居然藏着这么多工程奥秘。");
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
                ADVRewardPopup.ShowReward(ModContent.ItemType<AssaultStockModule>(), 1, null,
                    appearDuration: 24, holdDuration: -1, giveDuration: 16, requireClick: true,
                    anchorProvider: () => {
                        var rect = DialogueUIRegistry.Current?.GetPanelRect() ?? Rectangle.Empty;
                        if (rect == Rectangle.Empty)
                            return new Vector2(Main.screenWidth / 2f, Main.screenHeight * 0.45f);
                        return new Vector2(rect.Center.X, rect.Y - 70f);
                    }, offset: Vector2.Zero);
            }
        }

        protected override bool IsGiftCompleted(ADVSave save) => save.Get<ShepelGiftData>().SkeletronPrimeGift;
        protected override void MarkGiftCompleted(ADVSave save) => save.Get<ShepelGiftData>().SkeletronPrimeGift = true;
        protected override bool StartScenarioInternal() => ScenarioManager.Start<ShepelSkeletronPrimeGift>();
    }
}

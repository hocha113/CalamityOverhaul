using CalamityOverhaul.Content.ADV.ADVRewardPopups;
using CalamityOverhaul.Content.ADV.DialogueBoxs;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Barrel;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Shepel.Gifts
{
    internal class ShepelEoCGift : ShepelGiftScenarioBase
    {
        public override string Key => nameof(ShepelEoCGift);
        public override int TargetBossID => NPCID.EyeofCthulhu;
        public static LocalizedText RoleName { get; private set; }
        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }

        public override void SetStaticDefaults() {
            RoleName = this.GetLocalization(nameof(RoleName), () => "SHPC");
            L0 = this.GetLocalization(nameof(L0), () => "主人，我在战斗过程中记录了克苏鲁之眼的光学追踪数据。");
            L1 = this.GetLocalization(nameof(L1), () => "它用于锁定目标的晶体结构很有参考价值，我花了点时间将其转化成了SHPC的射击优化方案。");
            L2 = this.GetLocalization(nameof(L2), () => "拿着，这是激光枪管模组。它会让光束在命中目标前维持更高的聚焦精度。");
            L3 = this.GetLocalization(nameof(L3), () => "算是初期试炼的一点纪念物。");
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
                ADVRewardPopup.ShowReward(ModContent.ItemType<LaserBarrelModule>(), 1, null,
                    appearDuration: 24, holdDuration: -1, giveDuration: 16, requireClick: true,
                    anchorProvider: () => {
                        var rect = DialogueUIRegistry.Current?.GetPanelRect() ?? Rectangle.Empty;
                        if (rect == Rectangle.Empty)
                            return new Vector2(Main.screenWidth / 2f, Main.screenHeight * 0.45f);
                        return new Vector2(rect.Center.X, rect.Y - 70f);
                    }, offset: Vector2.Zero);
            }
        }

        protected override bool IsGiftCompleted(ADVSave save) => save.Get<ShepelGiftData>().EyeOfCthulhuGift;
        protected override void MarkGiftCompleted(ADVSave save) => save.Get<ShepelGiftData>().EyeOfCthulhuGift = true;
        protected override bool StartScenarioInternal() => ScenarioManager.Start<ShepelEoCGift>();
    }
}

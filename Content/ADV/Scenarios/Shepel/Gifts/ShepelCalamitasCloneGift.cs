using CalamityOverhaul.Content.ADV.ADVRewardPopups;
using CalamityOverhaul.Content.ADV.DialogueBoxs;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Barrel;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Shepel.Gifts
{
    internal class ShepelCalamitasCloneGift : ShepelGiftScenarioBase
    {
        public override string Key => nameof(ShepelCalamitasCloneGift);
        public override int TargetBossID => CWRID.NPC_CalamitasClone;
        public static LocalizedText RoleName { get; private set; }
        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }

        public override void SetStaticDefaults() {
            RoleName = this.GetLocalization(nameof(RoleName), () => "SHPC");
            L0 = this.GetLocalization(nameof(L0), () => "主人，灾厄克隆体的能量性质非常特殊——不是完全的灾厄能量，而是一种反射式的镜像干扰场。");
            L1 = this.GetLocalization(nameof(L1), () => "这种反射特性对于枪管设计很有参考价值，我整理成了一个光束导向配置。");
            L2 = this.GetLocalization(nameof(L2), () => "反射枪管模组，光束在命中后有几率产生反弹效果。");
            L3 = this.GetLocalization(nameof(L3), () => "克隆体的数据用途比原体更多，某种程度上反而更有价值。");
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
                ADVRewardPopup.ShowReward(ModContent.ItemType<ReflectionBarrelModule>(), 1, null,
                    appearDuration: 24, holdDuration: -1, giveDuration: 16, requireClick: true,
                    anchorProvider: () => {
                        var rect = DialogueUIRegistry.Current?.GetPanelRect() ?? Rectangle.Empty;
                        if (rect == Rectangle.Empty)
                            return new Vector2(Main.screenWidth / 2f, Main.screenHeight * 0.45f);
                        return new Vector2(rect.Center.X, rect.Y - 70f);
                    }, offset: Vector2.Zero);
            }
        }

        protected override bool IsGiftCompleted(ADVSave save) => save.Get<ShepelGiftData>().CalamitasCloneGift;
        protected override void MarkGiftCompleted(ADVSave save) => save.Get<ShepelGiftData>().CalamitasCloneGift = true;
        protected override bool StartScenarioInternal() => ScenarioManager.Start<ShepelCalamitasCloneGift>();
    }
}

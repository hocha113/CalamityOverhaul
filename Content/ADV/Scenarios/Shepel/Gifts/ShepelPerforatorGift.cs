using CalamityOverhaul.Content.ADV.ADVRewardPopups;
using CalamityOverhaul.Content.ADV.DialogueBoxs;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Barrel;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Shepel.Gifts
{
    internal class ShepelPerforatorGift : ShepelGiftScenarioBase
    {
        public override string Key => nameof(ShepelPerforatorGift);
        public override int TargetBossID => CWRID.NPC_PerforatorHive;
        public static LocalizedText RoleName { get; private set; }
        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }

        public override void SetStaticDefaults() {
            RoleName = this.GetLocalization(nameof(RoleName), () => "SHPC");
            L0 = this.GetLocalization(nameof(L0), () => "主人，血肉宿主的穿刺轨迹模型非常具有参考性——多点同步侵入，路径之间的夹角经过精密计算。");
            L1 = this.GetLocalization(nameof(L1), () => "我把这种弹道分布逻辑集成进了一个枪管配置里。");
            L2 = this.GetLocalization(nameof(L2), () => "散射枪管模组，拿着。单次射击可以分裂出多束光束覆盖较宽的范围。");
            L3 = this.GetLocalization(nameof(L3), () => "它的侵略性设计思路……我研究时保持了一点距离。");
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
                ADVRewardPopup.ShowReward(ModContent.ItemType<ScattershotBarrelModule>(), 1, null,
                    appearDuration: 24, holdDuration: -1, giveDuration: 16, requireClick: true,
                    anchorProvider: () => {
                        var rect = DialogueUIRegistry.Current?.GetPanelRect() ?? Rectangle.Empty;
                        if (rect == Rectangle.Empty)
                            return new Vector2(Main.screenWidth / 2f, Main.screenHeight * 0.45f);
                        return new Vector2(rect.Center.X, rect.Y - 70f);
                    }, offset: Vector2.Zero);
            }
        }

        protected override bool IsGiftCompleted(ADVSave save) => save.Get<ShepelGiftData>().PerforatorGift;
        protected override void MarkGiftCompleted(ADVSave save) => save.Get<ShepelGiftData>().PerforatorGift = true;
        protected override bool StartScenarioInternal() => ScenarioManager.Start<ShepelPerforatorGift>();
    }
}

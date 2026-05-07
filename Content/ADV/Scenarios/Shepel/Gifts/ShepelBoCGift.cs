using CalamityOverhaul.Content.ADV.ADVRewardPopups;
using CalamityOverhaul.Content.ADV.DialogueBoxs;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Grip;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Shepel.Gifts
{
    internal class ShepelBoCGift : ShepelGiftScenarioBase
    {
        public override string Key => nameof(ShepelBoCGift);
        public override int TargetBossID => NPCID.BrainofCthulhu;
        public static LocalizedText RoleName { get; private set; }
        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }

        public override void SetStaticDefaults() {
            RoleName = this.GetLocalization(nameof(RoleName), () => "SHPC");
            L0 = this.GetLocalization(nameof(L0), () => "主人，克苏鲁之脑的神经晶体在高速感知方面有异乎寻常的性能。");
            L1 = this.GetLocalization(nameof(L1), () => "那些晶体的传导率……我研究了一会儿，然后用相似的材料配比做了个新握把。");
            L2 = this.GetLocalization(nameof(L2), () => "水晶握把模组，拿着。使用SHPC时握持手感会有明显改善。");
            L3 = this.GetLocalization(nameof(L3), () => "说实话，那颗大脑让我研究得有点入神。数据太有趣了。");
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
                ADVRewardPopup.ShowReward(ModContent.ItemType<CrystalGripModule>(), 1, null,
                    appearDuration: 24, holdDuration: -1, giveDuration: 16, requireClick: true,
                    anchorProvider: () => {
                        var rect = DialogueUIRegistry.Current?.GetPanelRect() ?? Rectangle.Empty;
                        if (rect == Rectangle.Empty)
                            return new Vector2(Main.screenWidth / 2f, Main.screenHeight * 0.45f);
                        return new Vector2(rect.Center.X, rect.Y - 70f);
                    }, offset: Vector2.Zero);
            }
        }

        protected override bool IsGiftCompleted(ADVSave save) => save.Get<ShepelGiftData>().BrainOfCthulhuGift;
        protected override void MarkGiftCompleted(ADVSave save) => save.Get<ShepelGiftData>().BrainOfCthulhuGift = true;
        protected override bool StartScenarioInternal() => ScenarioManager.Start<ShepelBoCGift>();
    }
}

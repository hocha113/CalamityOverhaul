using CalamityOverhaul.Content.ADV.ADVRewardPopups;
using CalamityOverhaul.Content.ADV.DialogueBoxs;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Grip;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Shepel.Gifts
{
    internal class ShepelSlimeGodGift : ShepelGiftScenarioBase
    {
        public override string Key => nameof(ShepelSlimeGodGift);
        public override int TargetBossID => CWRID.NPC_SlimeGodCore;
        public static LocalizedText RoleName { get; private set; }
        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }

        public override void SetStaticDefaults() {
            RoleName = this.GetLocalization(nameof(RoleName), () => "SHPC");
            L0 = this.GetLocalization(nameof(L0), () => "主人，史莱姆之神的凝胶体有一种非常均匀的内部应力分布结构——它是真的。");
            L1 = this.GetLocalization(nameof(L1), () => "我利用这个特性重新设计了握把的力学平衡方案，让SHPC在快速转向时稳定性更高。");
            L2 = this.GetLocalization(nameof(L2), () => "平衡握把模组。任何方向的操作响应都会更一致一些。");
            L3 = this.GetLocalization(nameof(L3), () => "没想到一坨凝胶能给我这么多启发。记录在案了。");
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
                ADVRewardPopup.ShowReward(ModContent.ItemType<BalancedGripModule>(), 1, null,
                    appearDuration: 24, holdDuration: -1, giveDuration: 16, requireClick: true,
                    anchorProvider: () => {
                        var rect = DialogueUIRegistry.Current?.GetPanelRect() ?? Rectangle.Empty;
                        if (rect == Rectangle.Empty)
                            return new Vector2(Main.screenWidth / 2f, Main.screenHeight * 0.45f);
                        return new Vector2(rect.Center.X, rect.Y - 70f);
                    }, offset: Vector2.Zero);
            }
        }

        protected override bool IsGiftCompleted(ADVSave save) => save.Get<ShepelGiftData>().SlimeGodGift;
        protected override void MarkGiftCompleted(ADVSave save) => save.Get<ShepelGiftData>().SlimeGodGift = true;
        protected override bool StartScenarioInternal() => ScenarioManager.Start<ShepelSlimeGodGift>();
    }
}

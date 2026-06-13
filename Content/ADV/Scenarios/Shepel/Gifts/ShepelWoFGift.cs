using CalamityOverhaul.Content.ADV.ADVRewardPopups;
using CalamityOverhaul.Content.ADV.DialogueBoxs;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Grip;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Shepel.Gifts
{
    internal class ShepelWoFGift : ShepelGiftScenarioBase
    {
        public override string Key => nameof(ShepelWoFGift);
        public override int TargetBossID => NPCID.WallofFlesh;
        public static LocalizedText RoleName { get; private set; }
        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }

        public override void SetStaticDefaults() {
            RoleName = this.GetLocalization(nameof(RoleName), () => "SHPC");
            L0 = this.GetLocalization(nameof(L0), () => "肉墙倒塌后，地壳结构和能量基线都发生了剧烈的震荡。");
            L1 = this.GetLocalization(nameof(L1), () => "世界正在进入一个更危险的阶段。但同时，新的稀有材料也开始大量涌现。");
            L2 = this.GetLocalization(nameof(L2), () => "我用那些新出现的合金为您升级了握持系统，这算是我们跨入新阶段的第一步。");
            L3 = this.GetLocalization(nameof(L3), () => "无论外部环境发生怎样的异变，我的护卫协议永远不会动摇。");
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
                ADVRewardPopup.ShowReward(ModContent.ItemType<HarmonyGripModule>(), 1, null,
                    appearDuration: 24, holdDuration: -1, giveDuration: 16, requireClick: true,
                    anchorProvider: () => {
                        var rect = DialogueUIRegistry.Current?.GetPanelRect() ?? Rectangle.Empty;
                        if (rect == Rectangle.Empty)
                            return new Vector2(Main.screenWidth / 2f, Main.screenHeight * 0.45f);
                        return new Vector2(rect.Center.X, rect.Y - 70f);
                    }, offset: Vector2.Zero, styleProvider: () => ADVRewardPopup.RewardStyle.SHPC);
            }
        }

        protected override bool IsGiftCompleted(ADVSave save) => save.Get<ShepelGiftData>().WallOfFleshGift;
        protected override void MarkGiftCompleted(ADVSave save) => save.Get<ShepelGiftData>().WallOfFleshGift = true;
        protected override bool StartScenarioInternal() => ScenarioManager.Start<ShepelWoFGift>();
    }
}

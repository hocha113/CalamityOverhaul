using CalamityOverhaul.Content.ADV.ADVRewardPopups;
using CalamityOverhaul.Content.ADV.DialogueBoxs;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Frame;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Shepel.Gifts
{
    internal class ShepelPolterghastGift : ShepelGiftScenarioBase
    {
        public override string Key => nameof(ShepelPolterghastGift);
        public override int TargetBossID => CWRID.NPC_Polterghast;
        public static LocalizedText RoleName { get; private set; }
        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }

        public override void SetStaticDefaults() {
            RoleName = this.GetLocalization(nameof(RoleName), () => "SHPC");
            L0 = this.GetLocalization(nameof(L0), () => "主人，噬魂幽花的能量体在消散前发出了一段极为复杂的递归信号，类似无限嵌套的循环协议。");
            L1 = this.GetLocalization(nameof(L1), () => "我花了一些时间才把这段信号完整解码，然后将其转化成了一个机匣扩展算法。");
            L2 = this.GetLocalization(nameof(L2), () => "递归机匣模组，它让SHPC的攻击序列在特定条件下能够触发次级连锁反应。");
            L3 = this.GetLocalization(nameof(L3), () => "幽灵的'思考方式'比我预期的更具逻辑性。有些出乎意料。");
        }

        protected override void Build() {
            DialogueBoxBase.RegisterPortrait(RoleName.Value, texture: null);
            DialogueBoxBase.SetPortraitStyle(RoleName.Value, silhouette: false);
            Add(RoleName.Value, L0.Value,
                onStart: () => ShowPortraitWithFace(ShepelFullBodyPortrait.Face.None));
            Add(RoleName.Value, L1.Value);
            Add(RoleName.Value, L2.Value);
            Add(RoleName.Value, L3.Value,
                onStart: () => SetPortraitFace(ShepelFullBodyPortrait.Face.Blank),
                onComplete: Complete);
        }

        public override void PreProcessSegment(DialoguePreProcessArgs args) {
            if (args.Index == 2) {
                ADVRewardPopup.ShowReward(ModContent.ItemType<RecursiveFrameModule>(), 1, null,
                    appearDuration: 24, holdDuration: -1, giveDuration: 16, requireClick: true,
                    anchorProvider: () => {
                        var rect = DialogueUIRegistry.Current?.GetPanelRect() ?? Rectangle.Empty;
                        if (rect == Rectangle.Empty)
                            return new Vector2(Main.screenWidth / 2f, Main.screenHeight * 0.45f);
                        return new Vector2(rect.Center.X, rect.Y - 70f);
                    }, offset: Vector2.Zero, styleProvider: () => ADVRewardPopup.RewardStyle.SHPC);
            }
        }

        protected override bool IsGiftCompleted(ADVSave save) => save.Get<ShepelGiftData>().PolterghastGift;
        protected override void MarkGiftCompleted(ADVSave save) => save.Get<ShepelGiftData>().PolterghastGift = true;
        protected override bool StartScenarioInternal() => ScenarioManager.Start<ShepelPolterghastGift>();
    }
}

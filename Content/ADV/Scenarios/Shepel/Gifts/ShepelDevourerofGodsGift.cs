using CalamityOverhaul.Content.ADV.ADVRewardPopups;
using CalamityOverhaul.Content.ADV.DialogueBoxs;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Frame;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Shepel.Gifts
{
    internal class ShepelDevourerofGodsGift : ShepelGiftScenarioBase
    {
        public override string Key => nameof(ShepelDevourerofGodsGift);
        public override int TargetBossID => CWRID.NPC_DevourerofGodsHead;
        public static LocalizedText RoleName { get; private set; }
        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }

        public override void SetStaticDefaults() {
            RoleName = this.GetLocalization(nameof(RoleName), () => "SHPC");
            L0 = this.GetLocalization(nameof(L0), () => "主人，噬神者……那条东西的运动轨迹跨越了多个维度，我的追踪模型在战斗中途完全失效了。");
            L1 = this.GetLocalization(nameof(L1), () => "事后我重建了那段跨维度弹道数据，把其中的量子穿透特性做成了一个机匣模组。");
            L2 = this.GetLocalization(nameof(L2), () => "量子穿透机匣，它赋予了SHPC光束有限的相位穿透能力。");
            L3 = this.GetLocalization(nameof(L3), () => "数据量……远超正常上限。已完成整理，但处理时间比预期长了很多。");
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
                ADVRewardPopup.ShowReward(ModContent.ItemType<QuantumFrameModule>(), 1, null,
                    appearDuration: 24, holdDuration: -1, giveDuration: 16, requireClick: true,
                    anchorProvider: () => {
                        var rect = DialogueUIRegistry.Current?.GetPanelRect() ?? Rectangle.Empty;
                        if (rect == Rectangle.Empty)
                            return new Vector2(Main.screenWidth / 2f, Main.screenHeight * 0.45f);
                        return new Vector2(rect.Center.X, rect.Y - 70f);
                    }, offset: Vector2.Zero);
            }
        }

        protected override bool IsGiftCompleted(ADVSave save) => save.Get<ShepelGiftData>().DevourerofGodsGift;
        protected override void MarkGiftCompleted(ADVSave save) => save.Get<ShepelGiftData>().DevourerofGodsGift = true;
        protected override bool StartScenarioInternal() => ScenarioManager.Start<ShepelDevourerofGodsGift>();
    }
}

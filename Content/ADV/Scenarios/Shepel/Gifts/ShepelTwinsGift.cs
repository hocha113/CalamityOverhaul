using CalamityOverhaul.Content.ADV.ADVRewardPopups;
using CalamityOverhaul.Content.ADV.DialogueBoxs;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Optic;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Shepel.Gifts
{
    internal class ShepelTwinsGift : ShepelGiftScenarioBase
    {
        public override string Key => nameof(ShepelTwinsGift);
        public override int TargetBossID => NPCID.Retinazer;
        public static LocalizedText RoleName { get; private set; }
        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }

        public override void SetStaticDefaults() {
            RoleName = this.GetLocalization(nameof(RoleName), () => "SHPC");
            L0 = this.GetLocalization(nameof(L0), () => "主人，双子的镜像追踪协议非常精妙——两套系统相互校正、协同锁定，几乎不存在盲区。");
            L1 = this.GetLocalization(nameof(L1), () => "我把这个双重反馈逻辑迁移进了一个光学模组里，让SHPC能对已命中目标进行二次扫描确认。");
            L2 = this.GetLocalization(nameof(L2), () => "回声光学模组，命中后会有轻微的信号反弹效果。");
            L3 = this.GetLocalization(nameof(L3), () => "两只眼睛的数据比一只要丰富得多。");
        }

        protected override bool AdditionalConditions(ADVSave save, Player player) {
            return !NPC.AnyNPCs(NPCID.Spazmatism);
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
                ADVRewardPopup.ShowReward(ModContent.ItemType<PrecisionOpticModule>(), 1, null,
                    appearDuration: 24, holdDuration: -1, giveDuration: 16, requireClick: true,
                    anchorProvider: () => {
                        var rect = DialogueUIRegistry.Current?.GetPanelRect() ?? Rectangle.Empty;
                        if (rect == Rectangle.Empty)
                            return new Vector2(Main.screenWidth / 2f, Main.screenHeight * 0.45f);
                        return new Vector2(rect.Center.X, rect.Y - 70f);
                    }, offset: Vector2.Zero);
            }
        }

        protected override bool IsGiftCompleted(ADVSave save) => save.Get<ShepelGiftData>().TwinsGift;
        protected override void MarkGiftCompleted(ADVSave save) => save.Get<ShepelGiftData>().TwinsGift = true;
        protected override bool StartScenarioInternal() => ScenarioManager.Start<ShepelTwinsGift>();
    }
}

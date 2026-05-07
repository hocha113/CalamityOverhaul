using CalamityOverhaul.Content.ADV.ADVRewardPopups;
using CalamityOverhaul.Content.ADV.DialogueBoxs;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Power;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Shepel.Gifts
{
    internal class ShepelProvidenceGift : ShepelGiftScenarioBase
    {
        public override string Key => nameof(ShepelProvidenceGift);
        public override int TargetBossID => CWRID.NPC_Providence;
        public static LocalizedText RoleName { get; private set; }
        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }

        public override void SetStaticDefaults() {
            RoleName = this.GetLocalization(nameof(RoleName), () => "SHPC");
            L0 = this.GetLocalization(nameof(L0), () => "主人，亵渎天神倒下的那一刻，有一种远超我量程的能量释放涌入了整个区域的数据流。");
            L1 = this.GetLocalization(nameof(L1), () => "我花了相当长的时间对那段数据进行脱敏处理，才将其中可用的部分提炼出来。");
            L2 = this.GetLocalization(nameof(L2), () => "过载能量核，它让SHPC的充能上限大幅提升，代价是热量管理压力也随之增加。");
            L3 = this.GetLocalization(nameof(L3), () => "神明级能量数据，归档完毕。新的参考系已经建立。");
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
                ADVRewardPopup.ShowReward(ModContent.ItemType<OverloadCoreModule>(), 1, null,
                    appearDuration: 24, holdDuration: -1, giveDuration: 16, requireClick: true,
                    anchorProvider: () => {
                        var rect = DialogueUIRegistry.Current?.GetPanelRect() ?? Rectangle.Empty;
                        if (rect == Rectangle.Empty)
                            return new Vector2(Main.screenWidth / 2f, Main.screenHeight * 0.45f);
                        return new Vector2(rect.Center.X, rect.Y - 70f);
                    }, offset: Vector2.Zero);
            }
        }

        protected override bool IsGiftCompleted(ADVSave save) => save.Get<ShepelGiftData>().ProvidenceGift;
        protected override void MarkGiftCompleted(ADVSave save) => save.Get<ShepelGiftData>().ProvidenceGift = true;
        protected override bool StartScenarioInternal() => ScenarioManager.Start<ShepelProvidenceGift>();
    }
}

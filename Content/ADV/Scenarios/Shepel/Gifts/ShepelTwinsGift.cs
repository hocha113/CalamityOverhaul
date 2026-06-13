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
            L0 = this.GetLocalization(nameof(L0), () => "机械双子眼的交叉火力覆盖相当严密。");
            L1 = this.GetLocalization(nameof(L1), () => "不过，只要我们的链接保持稳定，就不会给它们留下任何死角。");
            L2 = this.GetLocalization(nameof(L2), () => "我强化了瞄准系统的二次扫描能力，提升了锁定的可靠性。");
            L3 = this.GetLocalization(nameof(L3), () => "请放心把侧翼和背后交给我。");
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
                    }, offset: Vector2.Zero, styleProvider: () => ADVRewardPopup.RewardStyle.SHPC);
            }
        }

        protected override bool IsGiftCompleted(ADVSave save) => save.Get<ShepelGiftData>().TwinsGift;
        protected override void MarkGiftCompleted(ADVSave save) => save.Get<ShepelGiftData>().TwinsGift = true;
        protected override bool StartScenarioInternal() => ScenarioManager.Start<ShepelTwinsGift>();
    }
}

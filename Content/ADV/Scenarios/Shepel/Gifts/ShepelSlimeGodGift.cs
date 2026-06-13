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
            L0 = this.GetLocalization(nameof(L0), () => "居然能分裂成这么多块……这场战斗弄得满地都是黏糊糊的凝胶。");
            L1 = this.GetLocalization(nameof(L1), () => "不过，我发现这些史莱姆的内部结构非常有弹性。我借用了一点，给您的武器握把加了一层柔性缓冲。");
            L2 = this.GetLocalization(nameof(L2), () => "您再试着举起武器看看？重心的分布应该变得更舒服了。");
            L3 = this.GetLocalization(nameof(L3), () => "哪怕战况再怎么混乱，只要手感依旧稳固，您就能永远保持从容。");
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
                    }, offset: Vector2.Zero, styleProvider: () => ADVRewardPopup.RewardStyle.SHPC);
            }
        }

        protected override bool IsGiftCompleted(ADVSave save) => save.Get<ShepelGiftData>().SlimeGodGift;
        protected override void MarkGiftCompleted(ADVSave save) => save.Get<ShepelGiftData>().SlimeGodGift = true;
        protected override bool StartScenarioInternal() => ScenarioManager.Start<ShepelSlimeGodGift>();
    }
}

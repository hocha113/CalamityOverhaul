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
            AddReward(RoleName.Value, L2.Value, ModContent.ItemType<BalancedGripModule>(), style: ADVRewardPopup.RewardStyle.SHPC);
            Add(RoleName.Value, L3.Value,
                onStart: () => SetPortraitFace(ShepelFullBodyPortrait.Face.Happy),
                onComplete: Complete);
        }

        protected override bool IsGiftCompleted(ADVSave save) => save.Get<ShepelGiftData>().SlimeGodGift;
        protected override void MarkGiftCompleted(ADVSave save) => save.Get<ShepelGiftData>().SlimeGodGift = true;
        protected override bool StartScenarioInternal() => ScenarioManager.Start<ShepelSlimeGodGift>();
    }
}

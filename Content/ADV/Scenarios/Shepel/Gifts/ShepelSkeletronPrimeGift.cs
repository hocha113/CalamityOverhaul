using CalamityOverhaul.Content.ADV.ADVRewardPopups;
using CalamityOverhaul.Content.ADV.DialogueBoxs;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Stock;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Shepel.Gifts
{
    internal class ShepelSkeletronPrimeGift : ShepelGiftScenarioBase
    {
        public override string Key => nameof(ShepelSkeletronPrimeGift);
        public override int TargetBossID => NPCID.SkeletronPrime;
        public static LocalizedText RoleName { get; private set; }
        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }

        public override void SetStaticDefaults() {
            RoleName = this.GetLocalization(nameof(RoleName), () => "SHPC");
            L0 = this.GetLocalization(nameof(L0), () => "挥舞着四条手臂的重型骨架……攻击频率确实很快，但机动逻辑过于死板。");
            L1 = this.GetLocalization(nameof(L1), () => "它只是在机械地重复行为，根本无法适应您的战术变化。");
            L2 = this.GetLocalization(nameof(L2), () => "我提炼了它在切换多种武器时的优势，为您优化了武器的突击响应速度。");
            L3 = this.GetLocalization(nameof(L3), () => "缺乏学习能力的旧式机械，只能沦为我的数据养料。");
        }

        protected override void Build() {
            DialogueBoxBase.RegisterPortrait(RoleName.Value, texture: null);
            DialogueBoxBase.SetPortraitStyle(RoleName.Value, silhouette: false);
            Add(RoleName.Value, L0.Value,
                onStart: () => ShowPortraitWithFace(ShepelFullBodyPortrait.Face.None));
            Add(RoleName.Value, L1.Value);
            AddReward(RoleName.Value, L2.Value, ModContent.ItemType<AssaultStockModule>(), style: ADVRewardPopup.RewardStyle.SHPC);
            Add(RoleName.Value, L3.Value,
                onStart: () => SetPortraitFace(ShepelFullBodyPortrait.Face.Smirk),
                onComplete: Complete);
        }

        protected override bool IsGiftCompleted(ADVSave save) => save.Get<ShepelGiftData>().SkeletronPrimeGift;
        protected override void MarkGiftCompleted(ADVSave save) => save.Get<ShepelGiftData>().SkeletronPrimeGift = true;
        protected override bool StartScenarioInternal() => ScenarioManager.Start<ShepelSkeletronPrimeGift>();
    }
}

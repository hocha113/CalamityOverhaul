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
            L0 = this.GetLocalization(nameof(L0), () => "天神倒下时释放的热量真是恐怖。");
            L1 = this.GetLocalization(nameof(L1), () => "主人，请稍微退后几步，剩下的能量收尾和清扫工作请交给我。");
            L2 = this.GetLocalization(nameof(L2), () => "我将那股四溢的圣火压缩成了这枚小巧的蓄能核心，它能大幅提升您的能量周转率。");
            L3 = this.GetLocalization(nameof(L3), () => "充能效率非常棒，不过使用时请当心烫手。");
        }

        protected override void Build() {
            DialogueBoxBase.RegisterPortrait(RoleName.Value, texture: null);
            DialogueBoxBase.SetPortraitStyle(RoleName.Value, silhouette: false);
            Add(RoleName.Value, L0.Value,
                onStart: () => ShowPortraitWithFace(ShepelFullBodyPortrait.Face.None));
            Add(RoleName.Value, L1.Value);
            AddReward(RoleName.Value, L2.Value, ModContent.ItemType<OverloadCoreModule>(), style: ADVRewardPopup.RewardStyle.SHPC);
            Add(RoleName.Value, L3.Value, onComplete: Complete);
        }

        protected override bool IsGiftCompleted(ADVSave save) => save.Get<ShepelGiftData>().ProvidenceGift;
        protected override void MarkGiftCompleted(ADVSave save) => save.Get<ShepelGiftData>().ProvidenceGift = true;
        protected override bool StartScenarioInternal() => ScenarioManager.Start<ShepelProvidenceGift>();
    }
}

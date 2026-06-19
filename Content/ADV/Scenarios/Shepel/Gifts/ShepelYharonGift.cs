using CalamityOverhaul.Content.ADV.ADVRewardPopups;
using CalamityOverhaul.Content.ADV.DialogueBoxs;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Barrel;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Shepel.Gifts
{
    internal class ShepelYharonGift : ShepelGiftScenarioBase
    {
        public override string Key => nameof(ShepelYharonGift);
        public override int TargetBossID => CWRID.NPC_Yharon;
        public static LocalizedText RoleName { get; private set; }
        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }

        public override void SetStaticDefaults() {
            RoleName = this.GetLocalization(nameof(RoleName), () => "SHPC");
            L0 = this.GetLocalization(nameof(L0), () => "那条龙的飞行速度……已经突破了我的追踪极限。");
            L1 = this.GetLocalization(nameof(L1), () => "为了不让您暴露在它的高速扑击下，我在实战中重写了底层的预测模块。");
            L2 = this.GetLocalization(nameof(L2), () => "现在，您的武器初速和响应速度已经得到了显著提升，足以超越它的极速。");
            L3 = this.GetLocalization(nameof(L3), () => "为了能一直跟上您的脚步，我的系统随时准备突破自身的上限。");
        }

        protected override void Build() {
            DialogueBoxBase.RegisterPortrait(RoleName.Value, texture: null);
            DialogueBoxBase.SetPortraitStyle(RoleName.Value, silhouette: false);
            Add(RoleName.Value, L0.Value,
                onStart: () => ShowPortraitWithFace(ShepelFullBodyPortrait.Face.None));
            Add(RoleName.Value, L1.Value);
            AddReward(RoleName.Value, L2.Value, ModContent.ItemType<HypersonicBarrelModule>(), style: ADVRewardPopup.RewardStyle.SHPC);
            Add(RoleName.Value, L3.Value,
                onStart: () => SetPortraitFace(ShepelFullBodyPortrait.Face.Happy),
                onComplete: Complete);
        }

        protected override bool IsGiftCompleted(ADVSave save) => save.Get<ShepelGiftData>().YharonGift;
        protected override void MarkGiftCompleted(ADVSave save) => save.Get<ShepelGiftData>().YharonGift = true;
        protected override bool StartScenarioInternal() => ScenarioManager.Start<ShepelYharonGift>();
    }
}

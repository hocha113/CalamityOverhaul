using CalamityOverhaul.Content.ADV.ADVRewardPopups;
using CalamityOverhaul.Content.ADV.DialogueBoxs;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Stock;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Shepel.Gifts
{
    internal class ShepelGolemGift : ShepelGiftScenarioBase
    {
        public override string Key => nameof(ShepelGolemGift);
        public override int TargetBossID => NPCID.Golem;
        public static LocalizedText RoleName { get; private set; }
        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }

        public override void SetStaticDefaults() {
            RoleName = this.GetLocalization(nameof(RoleName), () => "SHPC");
            L0 = this.GetLocalization(nameof(L0), () => "神庙里的环境有些压抑。石巨人的攻击虽然笨重，但引发的地形震荡容易破坏射击平衡。");
            L1 = this.GetLocalization(nameof(L1), () => "为了应对这种高频的物理冲击，我对武器架构进行了重新评估。");
            L2 = this.GetLocalization(nameof(L2), () => "我为您的武装加装了最新的减震结构。");
            L3 = this.GetLocalization(nameof(L3), () => "这样即使在剧烈的环境晃动中，您也能保持最稳定的射击姿态。");
        }

        protected override void Build() {
            DialogueBoxBase.RegisterPortrait(RoleName.Value, texture: null);
            DialogueBoxBase.SetPortraitStyle(RoleName.Value, silhouette: false);
            Add(RoleName.Value, L0.Value,
                onStart: () => ShowPortraitWithFace(ShepelFullBodyPortrait.Face.None));
            Add(RoleName.Value, L1.Value);
            AddReward(RoleName.Value, L2.Value, ModContent.ItemType<KineticDamperModule>(), style: ADVRewardPopup.RewardStyle.SHPC);
            Add(RoleName.Value, L3.Value, onComplete: Complete);
        }

        protected override bool IsGiftCompleted(ADVSave save) => save.Get<ShepelGiftData>().GolemGift;
        protected override void MarkGiftCompleted(ADVSave save) => save.Get<ShepelGiftData>().GolemGift = true;
        protected override bool StartScenarioInternal() => ScenarioManager.Start<ShepelGolemGift>();
    }
}

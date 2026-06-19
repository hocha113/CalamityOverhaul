using CalamityOverhaul.Content.ADV.ADVRewardPopups;
using CalamityOverhaul.Content.ADV.DialogueBoxs;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Barrel;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Shepel.Gifts
{
    internal class ShepelHiveMindGift : ShepelGiftScenarioBase
    {
        public override string Key => nameof(ShepelHiveMindGift);
        public override int TargetBossID => CWRID.NPC_HiveMind;
        public static LocalizedText RoleName { get; private set; }
        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }

        public override void SetStaticDefaults() {
            RoleName = this.GetLocalization(nameof(RoleName), () => "SHPC");
            L0 = this.GetLocalization(nameof(L0), () => "那个大脑散发的波动，带有强烈的精神干扰。主人，您的脑波频率刚才有些异常。");
            L1 = this.GetLocalization(nameof(L1), () => "请深呼吸，将注意力集中在我的系统提示音上，屏蔽掉那些亵渎的低语。");
            L2 = this.GetLocalization(nameof(L2), () => "我把那种干扰波动进行了反向编译。现在，您的攻击也能附带撕裂敌方护盾的高频震荡。");
            L3 = this.GetLocalization(nameof(L3), () => "危机已解除。回去之后，请允许我为您泡一杯安神的茶。");
        }

        protected override void Build() {
            DialogueBoxBase.RegisterPortrait(RoleName.Value, texture: null);
            DialogueBoxBase.SetPortraitStyle(RoleName.Value, silhouette: false);
            Add(RoleName.Value, L0.Value,
                onStart: () => ShowPortraitWithFace(ShepelFullBodyPortrait.Face.None));
            Add(RoleName.Value, L1.Value);
            AddReward(RoleName.Value, L2.Value, ModContent.ItemType<OscillatorBarrelModule>(), style: ADVRewardPopup.RewardStyle.SHPC);
            Add(RoleName.Value, L3.Value, onComplete: Complete);
        }

        protected override bool IsGiftCompleted(ADVSave save) => save.Get<ShepelGiftData>().HiveMindGift;
        protected override void MarkGiftCompleted(ADVSave save) => save.Get<ShepelGiftData>().HiveMindGift = true;
        protected override bool StartScenarioInternal() => ScenarioManager.Start<ShepelHiveMindGift>();
    }
}

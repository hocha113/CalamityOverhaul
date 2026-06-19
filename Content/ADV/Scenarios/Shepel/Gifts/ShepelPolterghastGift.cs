using CalamityOverhaul.Content.ADV.ADVRewardPopups;
using CalamityOverhaul.Content.ADV.DialogueBoxs;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Frame;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Shepel.Gifts
{
    internal class ShepelPolterghastGift : ShepelGiftScenarioBase
    {
        public override string Key => nameof(ShepelPolterghastGift);
        public override int TargetBossID => CWRID.NPC_Polterghast;
        public static LocalizedText RoleName { get; private set; }
        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }

        public override void SetStaticDefaults() {
            RoleName = this.GetLocalization(nameof(RoleName), () => "SHPC");
            L0 = this.GetLocalization(nameof(L0), () => "地牢里的这些幽魂，一直无法解脱，真是可悲。");
            L1 = this.GetLocalization(nameof(L1), () => "这里的环境有些嘈杂，我已经为您开启了噪音过滤。");
            L2 = this.GetLocalization(nameof(L2), () => "我捕捉了那些游荡的灵体信号，将其改写成了能连续触发次级火力的程序。");
            L3 = this.GetLocalization(nameof(L3), () => "将这些无序的残影转化为您的火力，或许是它们最合理的归宿。");
        }

        protected override void Build() {
            DialogueBoxBase.RegisterPortrait(RoleName.Value, texture: null);
            DialogueBoxBase.SetPortraitStyle(RoleName.Value, silhouette: false);
            Add(RoleName.Value, L0.Value,
                onStart: () => ShowPortraitWithFace(ShepelFullBodyPortrait.Face.None));
            Add(RoleName.Value, L1.Value);
            AddReward(RoleName.Value, L2.Value, ModContent.ItemType<RecursiveFrameModule>(), style: ADVRewardPopup.RewardStyle.SHPC);
            Add(RoleName.Value, L3.Value,
                onStart: () => SetPortraitFace(ShepelFullBodyPortrait.Face.Blank),
                onComplete: Complete);
        }

        protected override bool IsGiftCompleted(ADVSave save) => save.Get<ShepelGiftData>().PolterghastGift;
        protected override void MarkGiftCompleted(ADVSave save) => save.Get<ShepelGiftData>().PolterghastGift = true;
        protected override bool StartScenarioInternal() => ScenarioManager.Start<ShepelPolterghastGift>();
    }
}

using CalamityOverhaul.Content.ADV.ADVRewardPopups;
using CalamityOverhaul.Content.ADV.DialogueBoxs;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Stock;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Shepel.Gifts
{
    internal class ShepelEoWGift : ShepelGiftScenarioBase
    {
        public override string Key => nameof(ShepelEoWGift);
        public override int TargetBossID => NPCID.EaterofWorldsHead;
        public static LocalizedText RoleName { get; private set; }
        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }

        public override void SetStaticDefaults() {
            RoleName = this.GetLocalization(nameof(RoleName), () => "SHPC");
            L0 = this.GetLocalization(nameof(L0), () => "腐化地的气味真是难闻。主人，您的装甲缝隙里沾到了一些可疑的黏液，让我为您清理干净。");
            L1 = this.GetLocalization(nameof(L1), () => "在这种泥泞的地方作战，弄脏衣服真是让人头疼。");
            L2 = this.GetLocalization(nameof(L2), () => "我把枪托的配重稍微调轻了一些。");
            L3 = this.GetLocalization(nameof(L3), () => "这样您使用时就不那么容易溅起地上的污泥了。");
        }

        protected override void Build() {
            DialogueBoxBase.RegisterPortrait(RoleName.Value, texture: null);
            DialogueBoxBase.SetPortraitStyle(RoleName.Value, silhouette: false);
            Add(RoleName.Value, L0.Value,
                onStart: () => ShowPortraitWithFace(ShepelFullBodyPortrait.Face.None));
            Add(RoleName.Value, L1.Value);
            AddReward(RoleName.Value, L2.Value, ModContent.ItemType<LightStockModule>(), style: ADVRewardPopup.RewardStyle.SHPC);
            Add(RoleName.Value, L3.Value, onComplete: Complete);
        }

        protected override bool IsGiftCompleted(ADVSave save) => save.Get<ShepelGiftData>().EaterOfWorldsGift;
        protected override void MarkGiftCompleted(ADVSave save) => save.Get<ShepelGiftData>().EaterOfWorldsGift = true;
        protected override bool StartScenarioInternal() => ScenarioManager.Start<ShepelEoWGift>();
    }
}

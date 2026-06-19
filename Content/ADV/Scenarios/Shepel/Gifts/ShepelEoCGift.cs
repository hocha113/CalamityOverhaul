using CalamityOverhaul.Content.ADV.ADVRewardPopups;
using CalamityOverhaul.Content.ADV.DialogueBoxs;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Barrel;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Shepel.Gifts
{
    internal class ShepelEoCGift : ShepelGiftScenarioBase
    {
        public override string Key => nameof(ShepelEoCGift);
        public override int TargetBossID => NPCID.EyeofCthulhu;
        public static LocalizedText RoleName { get; private set; }
        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }

        public override void SetStaticDefaults() {
            RoleName = this.GetLocalization(nameof(RoleName), () => "SHPC");
            L0 = this.GetLocalization(nameof(L0), () => "夜晚的视线有些模糊呢，主人。不过那只巨大的眼睛已经被您彻底击碎了。");
            L1 = this.GetLocalization(nameof(L1), () => "我已经为您准备好了热茶和干净的毛巾，请稍作休整。");
            L2 = this.GetLocalization(nameof(L2), () => "在您休息时，我会对武器的光学系统进行改进，光束会更加聚拢。");
            L3 = this.GetLocalization(nameof(L3), () => "这只是旅途的开始。无论接下来去哪，我都会为您打理好一切后勤与武装。");
        }

        protected override void Build() {
            DialogueBoxBase.RegisterPortrait(RoleName.Value, texture: null);
            DialogueBoxBase.SetPortraitStyle(RoleName.Value, silhouette: false);
            Add(RoleName.Value, L0.Value,
                onStart: () => ShowPortraitWithFace(ShepelFullBodyPortrait.Face.None));
            Add(RoleName.Value, L1.Value);
            AddReward(RoleName.Value, L2.Value, ModContent.ItemType<LaserBarrelModule>(), style: ADVRewardPopup.RewardStyle.SHPC);
            Add(RoleName.Value, L3.Value, onComplete: Complete);
        }

        protected override bool IsGiftCompleted(ADVSave save) => save.Get<ShepelGiftData>().EyeOfCthulhuGift;
        protected override void MarkGiftCompleted(ADVSave save) => save.Get<ShepelGiftData>().EyeOfCthulhuGift = true;
        protected override bool StartScenarioInternal() => ScenarioManager.Start<ShepelEoCGift>();
    }
}

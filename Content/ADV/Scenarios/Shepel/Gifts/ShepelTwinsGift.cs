using CalamityOverhaul.Content.ADV.ADVRewardPopups;
using CalamityOverhaul.Content.ADV.DialogueBoxs;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Optic;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Shepel.Gifts
{
    internal class ShepelTwinsGift : ShepelGiftScenarioBase
    {
        public override string Key => nameof(ShepelTwinsGift);
        public override int TargetBossID => NPCID.Retinazer;
        public static LocalizedText RoleName { get; private set; }
        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }

        public override void SetStaticDefaults() {
            RoleName = this.GetLocalization(nameof(RoleName), () => "SHPC");
            L0 = this.GetLocalization(nameof(L0), () => "机械双子眼的交叉火力覆盖相当严密。");
            L1 = this.GetLocalization(nameof(L1), () => "不过，只要我们的链接保持稳定，就不会给它们留下任何死角。");
            L2 = this.GetLocalization(nameof(L2), () => "我强化了瞄准系统的二次扫描能力，提升了锁定的可靠性。");
            L3 = this.GetLocalization(nameof(L3), () => "请放心把侧翼和背后交给我。");
        }

        protected override bool AdditionalConditions(ADVSave save, Player player) {
            return !NPC.AnyNPCs(NPCID.Spazmatism);
        }

        protected override void Build() {
            DialogueBoxBase.RegisterPortrait(RoleName.Value, texture: null);
            DialogueBoxBase.SetPortraitStyle(RoleName.Value, silhouette: false);
            Add(RoleName.Value, L0.Value,
                onStart: () => ShowPortraitWithFace(ShepelFullBodyPortrait.Face.None));
            Add(RoleName.Value, L1.Value);
            AddReward(RoleName.Value, L2.Value, ModContent.ItemType<PrecisionOpticModule>(), style: ADVRewardPopup.RewardStyle.SHPC);
            Add(RoleName.Value, L3.Value, onComplete: Complete);
        }

        protected override bool IsGiftCompleted(ADVSave save) => save.Get<ShepelGiftData>().TwinsGift;
        protected override void MarkGiftCompleted(ADVSave save) => save.Get<ShepelGiftData>().TwinsGift = true;
        protected override bool StartScenarioInternal() => ScenarioManager.Start<ShepelTwinsGift>();
    }
}

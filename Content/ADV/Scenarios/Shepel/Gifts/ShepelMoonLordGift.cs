using CalamityOverhaul.Content.ADV.ADVRewardPopups;
using CalamityOverhaul.Content.ADV.DialogueBoxs;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Power;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Shepel.Gifts
{
    internal class ShepelMoonLordGift : ShepelGiftScenarioBase
    {
        public override string Key => nameof(ShepelMoonLordGift);
        public override int TargetBossID => NPCID.MoonLordCore;
        public static LocalizedText RoleName { get; private set; }
        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }

        public override void SetStaticDefaults() {
            RoleName = this.GetLocalization(nameof(RoleName), () => "SHPC");
            L0 = this.GetLocalization(nameof(L0), () => "盘踞在星空之上的异形终于陨落了。");
            L1 = this.GetLocalization(nameof(L1), () => "确认主人生理体征平稳……太好了，警报解除。");
            L2 = this.GetLocalization(nameof(L2), () => "我从它残余的高维力场中提炼出了这个核心。");
            L3 = this.GetLocalization(nameof(L3), () => "无论前方的道路通向多远的深空，我都会作为您的护盾与向导，绝不偏航。");
        }

        protected override void Build() {
            DialogueBoxBase.RegisterPortrait(RoleName.Value, texture: null);
            DialogueBoxBase.SetPortraitStyle(RoleName.Value, silhouette: false);
            Add(RoleName.Value, L0.Value,
                onStart: () => ShowPortraitWithFace(ShepelFullBodyPortrait.Face.None));
            Add(RoleName.Value, L1.Value);
            AddReward(RoleName.Value, L2.Value, ModContent.ItemType<SingularityCoreModule>(), style: ADVRewardPopup.RewardStyle.SHPC);
            Add(RoleName.Value, L3.Value,
                onStart: () => SetPortraitFace(ShepelFullBodyPortrait.Face.Happy),
                onComplete: Complete);
        }

        protected override bool IsGiftCompleted(ADVSave save) => save.Get<ShepelGiftData>().MoonLordGift;
        protected override void MarkGiftCompleted(ADVSave save) => save.Get<ShepelGiftData>().MoonLordGift = true;
        protected override bool StartScenarioInternal() => ScenarioManager.Start<ShepelMoonLordGift>();
    }
}

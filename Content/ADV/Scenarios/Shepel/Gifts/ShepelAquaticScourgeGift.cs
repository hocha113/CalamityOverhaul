using CalamityOverhaul.Content.ADV.ADVRewardPopups;
using CalamityOverhaul.Content.ADV.DialogueBoxs;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Optic;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Shepel.Gifts
{
    internal class ShepelAquaticScourgeGift : ShepelGiftScenarioBase
    {
        public override string Key => nameof(ShepelAquaticScourgeGift);
        public override int TargetBossID => CWRID.NPC_AquaticScourgeHead;
        public static LocalizedText RoleName { get; private set; }
        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }

        public override void SetStaticDefaults() {
            RoleName = this.GetLocalization(nameof(RoleName), () => "SHPC");
            L0 = this.GetLocalization(nameof(L0), () => "主人，渊海的水质可真让人头疼。这些飘来飘去的浑浊杂质，实在是太碍眼了。");
            L1 = this.GetLocalization(nameof(L1), () => "刚才飞溅的酸液弄脏了您的装甲，不过请放心，我已经抢先一步为您清理得干干净净了。");
            L2 = this.GetLocalization(nameof(L2), () => "另外，为了不让这些深海环境再来捣乱，我稍微花了一点心思，给瞄准镜重新写了一套光学滤镜。");
            L3 = this.GetLocalization(nameof(L3), () => "毕竟，让您在任何地方都能保持清爽完美的视野，是我最引以为傲的工作之一呢。");
        }

        protected override void Build() {
            DialogueBoxBase.RegisterPortrait(RoleName.Value, texture: null);
            DialogueBoxBase.SetPortraitStyle(RoleName.Value, silhouette: false);
            Add(RoleName.Value, L0.Value,
                onStart: () => ShowPortraitWithFace(ShepelFullBodyPortrait.Face.None));
            Add(RoleName.Value, L1.Value);
            AddReward(RoleName.Value, L2.Value, ModContent.ItemType<HoloOpticModule>(), style: ADVRewardPopup.RewardStyle.SHPC);
            Add(RoleName.Value, L3.Value, onComplete: Complete);
        }

        protected override bool IsGiftCompleted(ADVSave save) => save.Get<ShepelGiftData>().AquaticScourgeGift;
        protected override void MarkGiftCompleted(ADVSave save) => save.Get<ShepelGiftData>().AquaticScourgeGift = true;
        protected override bool StartScenarioInternal() => ScenarioManager.Start<ShepelAquaticScourgeGift>();
    }
}

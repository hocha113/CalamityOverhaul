using CalamityOverhaul.Content.ADV.ADVRewardPopups;
using CalamityOverhaul.Content.ADV.DialogueBoxs;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Barrel;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Shepel.Gifts
{
    internal class ShepelCalamitasCloneGift : ShepelGiftScenarioBase
    {
        public override string Key => nameof(ShepelCalamitasCloneGift);
        public override int TargetBossID => CWRID.NPC_CalamitasClone;
        public static LocalizedText RoleName { get; private set; }
        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }

        public override void SetStaticDefaults() {
            RoleName = this.GetLocalization(nameof(RoleName), () => "SHPC");
            L0 = this.GetLocalization(nameof(L0), () => "那个克隆体终于消停了。虽然只是赝品，但她到处乱丢硫磺火球的坏习惯，还是挺让人头疼的。");
            L1 = this.GetLocalization(nameof(L1), () => "请您先退后几步，主人。地上这些残骸里还有一些火星在噼啪作响，这种危险的清扫工作交给我就好。");
            L2 = this.GetLocalization(nameof(L2), () => "趁着打扫的功夫，我把她那种魔法的追踪轨迹拆解了一下，塞进了您的光束导向模块里。");
            L3 = this.GetLocalization(nameof(L3), () => "有了它，以后您只需要站在最安全的距离外优雅地开火就好。");
        }

        protected override void Build() {
            DialogueBoxBase.RegisterPortrait(RoleName.Value, texture: null);
            DialogueBoxBase.SetPortraitStyle(RoleName.Value, silhouette: false);
            Add(RoleName.Value, L0.Value,
                onStart: () => ShowPortraitWithFace(ShepelFullBodyPortrait.Face.None));
            Add(RoleName.Value, L1.Value);
            AddReward(RoleName.Value, L2.Value, ModContent.ItemType<ScorchBarrelModule>(), style: ADVRewardPopup.RewardStyle.SHPC);
            Add(RoleName.Value, L3.Value, onComplete: Complete);
        }

        protected override bool IsGiftCompleted(ADVSave save) => save.Get<ShepelGiftData>().CalamitasCloneGift;
        protected override void MarkGiftCompleted(ADVSave save) => save.Get<ShepelGiftData>().CalamitasCloneGift = true;
        protected override bool StartScenarioInternal() => ScenarioManager.Start<ShepelCalamitasCloneGift>();
    }
}

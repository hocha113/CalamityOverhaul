using CalamityOverhaul.Content.ADV.ADVRewardPopups;
using CalamityOverhaul.Content.ADV.DialogueBoxs;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Frame;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Shepel.Gifts
{
    internal class ShepelCultistGift : ShepelGiftScenarioBase
    {
        public override string Key => nameof(ShepelCultistGift);
        public override int TargetBossID => NPCID.CultistBoss;
        public static LocalizedText RoleName { get; private set; }
        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }

        public override void SetStaticDefaults() {
            RoleName = this.GetLocalization(nameof(RoleName), () => "SHPC");
            L0 = this.GetLocalization(nameof(L0), () => "那些神神叨叨的信徒可算消停了，在这里搞些诡异的仪式。");
            L1 = this.GetLocalization(nameof(L1), () => "不过主人，您觉不觉得空气变得非常沉闷？我的传感器也在报警，好像有什么了不得的大麻烦正在从天上靠近。");
            L2 = this.GetLocalization(nameof(L2), () => "刚才我趁乱截获了那些乱七八糟的虚空信号，顺手把它改成了一个能帮您捕捉目标的小插件。");
            L3 = this.GetLocalization(nameof(L3), () => "看来接下来有一场硬仗要打，准备做好准备，主人，我随时都在。");
        }

        protected override void Build() {
            DialogueBoxBase.RegisterPortrait(RoleName.Value, texture: null);
            DialogueBoxBase.SetPortraitStyle(RoleName.Value, silhouette: false);
            Add(RoleName.Value, L0.Value,
                onStart: () => ShowPortraitWithFace(ShepelFullBodyPortrait.Face.None));
            Add(RoleName.Value, L1.Value);
            AddReward(RoleName.Value, L2.Value, ModContent.ItemType<PhantomFrameModule>(), style: ADVRewardPopup.RewardStyle.SHPC);
            Add(RoleName.Value, L3.Value, onComplete: Complete);
        }

        protected override bool IsGiftCompleted(ADVSave save) => save.Get<ShepelGiftData>().CultistGift;
        protected override void MarkGiftCompleted(ADVSave save) => save.Get<ShepelGiftData>().CultistGift = true;
        protected override bool StartScenarioInternal() => ScenarioManager.Start<ShepelCultistGift>();
    }
}

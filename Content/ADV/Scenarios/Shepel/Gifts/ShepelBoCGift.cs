using CalamityOverhaul.Content.ADV.ADVRewardPopups;
using CalamityOverhaul.Content.ADV.DialogueBoxs;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Grip;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Shepel.Gifts
{
    internal class ShepelBoCGift : ShepelGiftScenarioBase
    {
        public override string Key => nameof(ShepelBoCGift);
        public override int TargetBossID => NPCID.BrainofCthulhu;
        public static LocalizedText RoleName { get; private set; }
        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }

        public override void SetStaticDefaults() {
            RoleName = this.GetLocalization(nameof(RoleName), () => "SHPC");
            L0 = this.GetLocalization(nameof(L0), () => "和那种喜欢在脑子里制造幻觉的怪物交手，一定让您头晕脑胀了吧？真是个不讲礼貌的家伙。");
            L1 = this.GetLocalization(nameof(L1), () => "您的指尖还在轻微地发抖呢，已经为您调整了链接处的微电流，请稍微放松一下吧。");
            L2 = this.GetLocalization(nameof(L2), () => "至于那颗讨厌的大脑，我借用了它遗留下来的神经结晶，做了一点小小的材质改良。");
            L3 = this.GetLocalization(nameof(L3), () => "这是一个全新的防滑握把。有了它，哪怕是在精神紧绷的战斗中，武器也能安安稳稳地贴合您的掌心。");
        }

        protected override void Build() {
            DialogueBoxBase.RegisterPortrait(RoleName.Value, texture: null);
            DialogueBoxBase.SetPortraitStyle(RoleName.Value, silhouette: false);
            Add(RoleName.Value, L0.Value,
                onStart: () => ShowPortraitWithFace(ShepelFullBodyPortrait.Face.None));
            Add(RoleName.Value, L1.Value);
            AddReward(RoleName.Value, L2.Value, ModContent.ItemType<CrystalGripModule>(), style: ADVRewardPopup.RewardStyle.SHPC);
            Add(RoleName.Value, L3.Value,
                onStart: () => SetPortraitFace(ShepelFullBodyPortrait.Face.Smirk),
                onComplete: Complete);
        }

        protected override bool IsGiftCompleted(ADVSave save) => save.Get<ShepelGiftData>().BrainOfCthulhuGift;
        protected override void MarkGiftCompleted(ADVSave save) => save.Get<ShepelGiftData>().BrainOfCthulhuGift = true;
        protected override bool StartScenarioInternal() => ScenarioManager.Start<ShepelBoCGift>();
    }
}

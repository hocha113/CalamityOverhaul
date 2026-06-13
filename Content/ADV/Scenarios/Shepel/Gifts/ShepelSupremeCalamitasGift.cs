using CalamityOverhaul.Content.ADV.ADVRewardPopups;
using CalamityOverhaul.Content.ADV.DialogueBoxs;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Power;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Shepel.Gifts
{
    internal class ShepelSupremeCalamitasGift : ShepelGiftScenarioBase
    {
        public override string Key => nameof(ShepelSupremeCalamitasGift);
        public override int TargetBossID => CWRID.NPC_SupremeCalamitas;
        public static LocalizedText RoleName { get; private set; }
        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }

        public override void SetStaticDefaults() {
            RoleName = this.GetLocalization(nameof(RoleName), () => "SHPC");
            L0 = this.GetLocalization(nameof(L0), () => "焚海的魔女……终于落幕了。主人的各项生命体征一切正常，没有致命伤。");
            L1 = this.GetLocalization(nameof(L1), () => "确认全域威胁解除。呼……请允许我暂时挂起战斗协议，执行一次深度的系统自检。");
            L2 = this.GetLocalization(nameof(L2), () => "我收集了散落的混沌等离子，为您制作了这个最高效的能量模组。");
            L3 = this.GetLocalization(nameof(L3), () => "漫长的战役终于结束了。主人，接下来的和平时光，也请让我继续作为女仆服侍您。");
        }

        protected override void Build() {
            DialogueBoxBase.RegisterPortrait(RoleName.Value, texture: null);
            DialogueBoxBase.SetPortraitStyle(RoleName.Value, silhouette: false);
            Add(RoleName.Value, L0.Value,
                onStart: () => ShowPortraitWithFace(ShepelFullBodyPortrait.Face.None));
            Add(RoleName.Value, L1.Value);
            Add(RoleName.Value, L2.Value);
            Add(RoleName.Value, L3.Value,
                onStart: () => SetPortraitFace(ShepelFullBodyPortrait.Face.Happy),
                onComplete: Complete);
        }

        public override void PreProcessSegment(DialoguePreProcessArgs args) {
            if (args.Index == 2) {
                ADVRewardPopup.ShowReward(ModContent.ItemType<PlasmaInjectorModule>(), 1, null,
                    appearDuration: 24, holdDuration: -1, giveDuration: 16, requireClick: true,
                    anchorProvider: () => {
                        var rect = DialogueUIRegistry.Current?.GetPanelRect() ?? Rectangle.Empty;
                        if (rect == Rectangle.Empty)
                            return new Vector2(Main.screenWidth / 2f, Main.screenHeight * 0.45f);
                        return new Vector2(rect.Center.X, rect.Y - 70f);
                    }, offset: Vector2.Zero, styleProvider: () => ADVRewardPopup.RewardStyle.SHPC);
            }
        }

        protected override bool IsGiftCompleted(ADVSave save) => save.Get<ShepelGiftData>().SupremeCalamitasGift;
        protected override void MarkGiftCompleted(ADVSave save) => save.Get<ShepelGiftData>().SupremeCalamitasGift = true;
        protected override bool StartScenarioInternal() => ScenarioManager.Start<ShepelSupremeCalamitasGift>();
    }
}

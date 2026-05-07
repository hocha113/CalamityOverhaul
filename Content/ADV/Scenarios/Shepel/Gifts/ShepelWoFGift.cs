using CalamityOverhaul.Content.ADV.ADVRewardPopups;
using CalamityOverhaul.Content.ADV.DialogueBoxs;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Grip;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Shepel.Gifts
{
    internal class ShepelWoFGift : ShepelGiftScenarioBase
    {
        public override string Key => nameof(ShepelWoFGift);
        public override int TargetBossID => NPCID.WallofFlesh;
        public static LocalizedText RoleName { get; private set; }
        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }

        public override void SetStaticDefaults() {
            RoleName = this.GetLocalization(nameof(RoleName), () => "SHPC");
            L0 = this.GetLocalization(nameof(L0), () => "主人，壁障崩解后，世界底层规则发生了根本性的重构，有大量新材料随之浮现。");
            L1 = this.GetLocalization(nameof(L1), () => "我利用这次机会对SHPC的握持系统进行了一次全面升级，使用了几种之前根本无法获取的合金材料。");
            L2 = this.GetLocalization(nameof(L2), () => "谐振握把模组，这是困难模式门槛的解锁奖励之一。性能全面高于之前版本。");
            L3 = this.GetLocalization(nameof(L3), () => "世界改变了，我们也需要跟上它的节奏。");
        }

        protected override void Build() {
            DialogueBoxBase.RegisterPortrait(RoleName.Value, texture: null);
            DialogueBoxBase.SetPortraitStyle(RoleName.Value, silhouette: false);
            Add(RoleName.Value, L0.Value,
                onStart: () => ShowPortraitWithFace(ShepelFullBodyPortrait.Face.None));
            Add(RoleName.Value, L1.Value);
            Add(RoleName.Value, L2.Value);
            Add(RoleName.Value, L3.Value, onComplete: Complete);
        }

        public override void PreProcessSegment(DialoguePreProcessArgs args) {
            if (args.Index == 2) {
                ADVRewardPopup.ShowReward(ModContent.ItemType<HarmonyGripModule>(), 1, null,
                    appearDuration: 24, holdDuration: -1, giveDuration: 16, requireClick: true,
                    anchorProvider: () => {
                        var rect = DialogueUIRegistry.Current?.GetPanelRect() ?? Rectangle.Empty;
                        if (rect == Rectangle.Empty)
                            return new Vector2(Main.screenWidth / 2f, Main.screenHeight * 0.45f);
                        return new Vector2(rect.Center.X, rect.Y - 70f);
                    }, offset: Vector2.Zero);
            }
        }

        protected override bool IsGiftCompleted(ADVSave save) => save.Get<ShepelGiftData>().WallOfFleshGift;
        protected override void MarkGiftCompleted(ADVSave save) => save.Get<ShepelGiftData>().WallOfFleshGift = true;
        protected override bool StartScenarioInternal() => ScenarioManager.Start<ShepelWoFGift>();
    }
}

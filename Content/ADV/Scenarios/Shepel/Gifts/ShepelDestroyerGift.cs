using CalamityOverhaul.Content.ADV.ADVRewardPopups;
using CalamityOverhaul.Content.ADV.DialogueBoxs;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Stock;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Shepel.Gifts
{
    internal class ShepelDestroyerGift : ShepelGiftScenarioBase
    {
        public override string Key => nameof(ShepelDestroyerGift);
        public override int TargetBossID => NPCID.TheDestroyer;
        public static LocalizedText RoleName { get; private set; }
        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }

        public override void SetStaticDefaults() {
            RoleName = this.GetLocalization(nameof(RoleName), () => "SHPC");
            L0 = this.GetLocalization(nameof(L0), () => "主人，这台巨型机械的残骸我已经扫描完毕了。说实话，它的设计逻辑真是粗暴得毫无美感。");
            L1 = this.GetLocalization(nameof(L1), () => "明明拥有那么庞大的能源，却只知道像没头苍蝇一样横冲直撞，完全是在浪费性能。");
            L2 = this.GetLocalization(nameof(L2), () => "我从它报废的数据流里提取了一套很有意思的冲击力处理方式。");
            L3 = this.GetLocalization(nameof(L3), () => "这样一来，开火时的后坐力会变得可以利用。");
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
                ADVRewardPopup.ShowReward(ModContent.ItemType<RecoilStockModule>(), 1, null,
                    appearDuration: 24, holdDuration: -1, giveDuration: 16, requireClick: true,
                    anchorProvider: () => {
                        var rect = DialogueUIRegistry.Current?.GetPanelRect() ?? Rectangle.Empty;
                        if (rect == Rectangle.Empty)
                            return new Vector2(Main.screenWidth / 2f, Main.screenHeight * 0.45f);
                        return new Vector2(rect.Center.X, rect.Y - 70f);
                    }, offset: Vector2.Zero, styleProvider: () => ADVRewardPopup.RewardStyle.SHPC);
            }
        }

        protected override bool IsGiftCompleted(ADVSave save) => save.Get<ShepelGiftData>().DestroyerGift;
        protected override void MarkGiftCompleted(ADVSave save) => save.Get<ShepelGiftData>().DestroyerGift = true;
        protected override bool StartScenarioInternal() => ScenarioManager.Start<ShepelDestroyerGift>();
    }
}

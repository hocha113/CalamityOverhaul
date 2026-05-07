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
            L0 = this.GetLocalization(nameof(L0), () => "主人，毁灭者每次节点爆炸产生的后坐力是一种非常规律的周期性冲击波。");
            L1 = this.GetLocalization(nameof(L1), () => "我把这个冲击规律转化成了枪托的缓冲优化参数，将后坐力转变为可控的能量反馈。");
            L2 = this.GetLocalization(nameof(L2), () => "反冲枪托模组，持续射击时稳定性明显提升。");
            L3 = this.GetLocalization(nameof(L3), () => "把对方的攻击方式反过来用，算是战场工程学。");
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
                    }, offset: Vector2.Zero);
            }
        }

        protected override bool IsGiftCompleted(ADVSave save) => save.Get<ShepelGiftData>().DestroyerGift;
        protected override void MarkGiftCompleted(ADVSave save) => save.Get<ShepelGiftData>().DestroyerGift = true;
        protected override bool StartScenarioInternal() => ScenarioManager.Start<ShepelDestroyerGift>();
    }
}

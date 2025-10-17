using CalamityMod.NPCs.BrimstoneElemental;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.ADV.Scenarios.Gifts
{
    internal class BrimstoneElementalGift : GiftScenarioBase
    {
        public override string Key => nameof(BrimstoneElementalGift);
        public override int TargetBossID => ModContent.NPCType<BrimstoneElemental>();
        public static LocalizedText R1 { get; private set; }
        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }
        public static LocalizedText L4 { get; private set; }
        public override void SetStaticDefaults() {
            R1 = this.GetLocalization(nameof(R1), () => "比目鱼");
            L0 = this.GetLocalization(nameof(L0), () => "硫磺与火焰的化身，她的存在就是一场燃烧的演讲，不过我们还是成功让她闭嘴了");
            L1 = this.GetLocalization(nameof(L1), () => "有些元素不是被创造的，而是从世界的裂缝中渗出来的愤怒");
            L2 = this.GetLocalization(nameof(L2), () => "黑曜鱼，熔岩冷却的瞬间凝固产物。它的鳞片比仇恨还要坚硬");
            L3 = this.GetLocalization(nameof(L3), () => "小心，它可能会在你手里自燃，不是物理上的，而是概念上的");
            L4 = this.GetLocalization(nameof(L4), () => "毕竟有些愤怒是会传染的");
        }
        protected override void Build() {
            DialogueBoxBase.RegisterPortrait(R1.Value, ADVAsset.HeadADV);
            DialogueBoxBase.SetPortraitStyle(R1.Value, silhouette: false);
            Add(R1.Value, L0.Value);
            Add(R1.Value, L1.Value);
            Add(R1.Value, L2.Value); //奖励
            Add(R1.Value, L3.Value);
            Add(R1.Value, L4.Value);
        }
        public override void PreProcessSegment(DialogueBoxBase.DialoguePreProcessArgs args) {
            if (args.Index == 2) {
                ADVRewardPopup.ShowReward(ItemID.Obsidifish, 1, null, appearDuration: 24, holdDuration: -1, giveDuration: 16, requireClick: true,
                    anchorProvider: () => {
                        var rect = DialogueUIRegistry.Current?.GetPanelRect() ?? Rectangle.Empty;
                        if (rect == Rectangle.Empty) {
                            return new Vector2(Main.screenWidth / 2f, Main.screenHeight * 0.45f);
                        }
                        return new Vector2(rect.Center.X, rect.Y - 70f);
                    }, offset: Vector2.Zero);
            }
        }
        protected override bool IsGiftCompleted(ADVSave save) {
            return save.BrimstoneElementalGift;
        }
        protected override void MarkGiftCompleted(ADVSave save) {
            save.BrimstoneElementalGift = true;
        }
        protected override bool StartScenarioInternal() {
            return ScenarioManager.Start<BrimstoneElementalGift>();
        }
    }
}

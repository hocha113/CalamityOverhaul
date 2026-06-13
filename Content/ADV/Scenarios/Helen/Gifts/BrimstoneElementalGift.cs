using CalamityOverhaul.Content.ADV.ADVRewardPopups;
using CalamityOverhaul.Content.ADV.Common;
using CalamityOverhaul.Content.ADV.DialogueBoxs;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Helen.Gifts
{
    internal class BrimstoneElementalGift : GiftScenarioBase
    {
        public override string Key => nameof(BrimstoneElementalGift);
        public override int TargetBossID => CWRID.NPC_BrimstoneElemental;
        public static LocalizedText R1 { get; private set; }
        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }
        public static LocalizedText L4 { get; private set; }
        public override void SetStaticDefaults() {
            R1 = this.GetLocalization(nameof(R1), () => "比目鱼");
            L0 = this.GetLocalization(nameof(L0), () => "她往那里一站就像一场燃烧的演讲，幸好我们成功让她闭嘴了");
            L1 = this.GetLocalization(nameof(L1), () => "有些元素不是被创造的，而是从世界的裂缝中渗出来的古怪玩意儿");
            L2 = this.GetLocalization(nameof(L2), () => "黑曜石鱼，熔岩冷却的瞬间凝固产物。它的鳞片比地狱里纠缠的仇恨还要坚硬");
            L3 = this.GetLocalization(nameof(L3), () => "小心它在你手里自燃，情绪意义上的");
            L4 = this.GetLocalization(nameof(L4), () => "毕竟愤怒是会传染的");
        }
        protected override void Build() {
            DialogueBoxBase.RegisterPortrait(R1.Value, ADVAsset.HelenADV);
            DialogueBoxBase.SetPortraitStyle(R1.Value, silhouette: false);
            Add(R1.Value, L0.Value);
            Add(R1.Value, L1.Value);
            Add(R1.Value, L2.Value); //奖励
            Add(R1.Value, L3.Value);
            Add(R1.Value, L4.Value);
        }
        public override void PreProcessSegment(DialoguePreProcessArgs args) {
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
            return save.Get<BossGiftADVData>().BrimstoneElementalGift;
        }
        protected override void MarkGiftCompleted(ADVSave save) {
            save.Get<BossGiftADVData>().BrimstoneElementalGift = true;
        }
        protected override bool StartScenarioInternal() {
            return ScenarioManager.Start<BrimstoneElementalGift>();
        }
    }
}

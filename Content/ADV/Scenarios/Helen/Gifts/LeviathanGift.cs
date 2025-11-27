using CalamityOverhaul.Content.ADV.Common;
using CalamityOverhaul.Content.LegendWeapon.HalibutLegend;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Helen.Gifts
{
    internal class LeviathanGift : GiftScenarioBase
    {
        public override string Key => nameof(LeviathanGift);
        public override int TargetBossID => CWRID.NPC_Leviathan;
        public static LocalizedText R1 { get; private set; }
        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }
        public static LocalizedText L4 { get; private set; }
        public override void SetStaticDefaults() {
            R1 = this.GetLocalization(nameof(R1), () => "比目鱼");
            L0 = this.GetLocalization(nameof(L0), () => "海洋的暴君……和那个总是跟着她的小跟班。有些友谊超越了物种，也超越了理智");
            L1 = this.GetLocalization(nameof(L1), () => "你知道吗？最深的海沟里住着的不是恐惧，而是孤独。它们只是在寻找陪伴");
            L2 = this.GetLocalization(nameof(L2), () => "热带梭鱼，从深海漩涡里捞出来的。它看起来很普通，但这正是最可疑的地方");
            L3 = this.GetLocalization(nameof(L3), () => "越是平凡的外表，越是隐藏着不平凡的过去");
            L4 = this.GetLocalization(nameof(L4), () => "就像我们一样");
        }
        protected override void Build() {
            DialogueBoxBase.RegisterPortrait(R1.Value, ADVAsset.HelenADV);
            DialogueBoxBase.RegisterPortrait(R1.Value + " ", ADVAsset.Helen_enjoyADV);
            Add(R1.Value, L0.Value);
            Add(R1.Value + " ", L1.Value);
            Add(R1.Value, L2.Value); //奖励
            Add(R1.Value, L3.Value);
            Add(R1.Value, L4.Value);
        }
        public override void PreProcessSegment(DialogueBoxBase.DialoguePreProcessArgs args) {
            if (args.Index == 2) {
                ADVRewardPopup.ShowReward(ItemID.TropicalBarracuda, 1, null, appearDuration: 24, holdDuration: -1, giveDuration: 16, requireClick: true,
                    anchorProvider: () => {
                        var rect = DialogueUIRegistry.Current?.GetPanelRect() ?? Rectangle.Empty;
                        if (rect == Rectangle.Empty) {
                            return new Vector2(Main.screenWidth / 2f, Main.screenHeight * 0.45f);
                        }
                        return new Vector2(rect.Center.X, rect.Y - 70f);
                    }, offset: Vector2.Zero);
            }
        }
        protected override bool AdditionalConditions(ADVSave save, HalibutPlayer halibutPlayer) {
            return !NPC.AnyNPCs(CWRID.NPC_Anahita);//确保阿纳希塔也嘎了
        }
        protected override bool IsGiftCompleted(ADVSave save) {
            return save.LeviathanGift;
        }
        protected override void MarkGiftCompleted(ADVSave save) {
            save.LeviathanGift = true;
        }
        protected override bool StartScenarioInternal() {
            return ScenarioManager.Start<LeviathanGift>();
        }
    }
}

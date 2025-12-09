using CalamityOverhaul.Content.ADV.ADVRewardPopups;
using CalamityOverhaul.Content.ADV.Common;
using CalamityOverhaul.Content.ADV.DialogueBoxs;
using CalamityOverhaul.Content.ADV.Scenarios.SupCal.End.EternalBlazingNows;
using CalamityOverhaul.Content.LegendWeapon.HalibutLegend;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Helen.Gifts
{
    internal class SupremeCalamitasGift : GiftScenarioBase
    {
        public override string Key => nameof(SupremeCalamitasGift);
        public override int TargetBossID => CWRID.NPC_SupremeCalamitas;
        public static LocalizedText R1 { get; private set; }
        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }
        public static LocalizedText L4 { get; private set; }
        public override void SetStaticDefaults() {
            R1 = this.GetLocalization(nameof(R1), () => "比目鱼");
            L0 = this.GetLocalization(nameof(L0), () => "硫火女巫……她是复仇的化身，是被背叛淬炼出的完美武器");
            L1 = this.GetLocalization(nameof(L1), () => "我们刚才做的，是终结了一个传说。还是创造了一个新的开始？");
            L2 = this.GetLocalization(nameof(L2), () => "公主鱼，从灾厄余烬中诞生的。它带着一种矛盾的优雅，就像在废墟上盛开的花");
            L3 = this.GetLocalization(nameof(L3), () => "据说它能让人看到自己最想成为的样子，但那个样子往往是最不像自己的");
            L4 = this.GetLocalization(nameof(L4), () => "恭喜，你站在了力量的顶点，接下来……就是漫长的下坡路了");
        }
        protected override void Build() {
            DialogueBoxBase.RegisterPortrait(R1.Value, ADVAsset.HelenADV);
            DialogueBoxBase.RegisterPortrait(R1.Value + " ", ADVAsset.Helen_enjoyADV);
            DialogueBoxBase.RegisterPortrait(R1.Value + " " + " ", ADVAsset.Helen_seriousADV);
            Add(R1.Value, L0.Value);
            Add(R1.Value + " ", L1.Value);
            Add(R1.Value, L2.Value); //奖励
            Add(R1.Value, L3.Value);
            Add(R1.Value + " " + " ", L4.Value);
        }
        public override void PreProcessSegment(DialogueBoxBase.DialoguePreProcessArgs args) {
            if (args.Index == 2) {
                ADVRewardPopup.ShowReward(ItemID.PrincessFish, 1, null, appearDuration: 24, holdDuration: -1, giveDuration: 16, requireClick: true,
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
            return save.SupremeCalamitasGift;
        }
        protected override void MarkGiftCompleted(ADVSave save) {
            save.SupremeCalamitasGift = true;
        }
        protected override bool StartScenarioInternal() {
            return ScenarioManager.Start<SupremeCalamitasGift>();
        }
        protected override bool AdditionalConditions(ADVSave save, HalibutPlayer halibutPlayer) {
            return !EbnEffect.IsActive;//防止冲突
        }
    }
}

using CalamityMod.Items.Fishing.BrimstoneCragCatches;
using CalamityMod.NPCs.CalClone;
using CalamityOverhaul.Content.ADV.Scenarios.Common;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Helen.Gifts
{
    internal class CalamitasCloneGift : GiftScenarioBase
    {
        public override string Key => nameof(CalamitasCloneGift);
        public override int TargetBossID => ModContent.NPCType<CalamitasClone>();
        public static LocalizedText R1 { get; private set; }
        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }
        public static LocalizedText L4 { get; private set; }
        private const string solemn = " ";
        private const string serious = " " + " ";
        private const string naughty = " " + " " + " ";
        private const string enjoy = " " + " " + " " + " ";
        public override void SetStaticDefaults() {
            R1 = this.GetLocalization(nameof(R1), () => "比目鱼");
            L0 = this.GetLocalization(nameof(L0), () => "一个劣质的影子，让我想起来上大学时自己窝在海沟里捣鼓的基因怪兽，总之制作这个东西的家伙品味很差");
            L1 = this.GetLocalization(nameof(L1), () => "我有种不详的预感，真正的恐怖还在更深的地方等着，它甚至在靠近");
            L2 = this.GetLocalization(nameof(L2), () => "说回正事，硫磺火鱼，从灾厄的余烬中捡的。捧在手心里还能听到它在瞎嘀咕些什么");
            L3 = this.GetLocalization(nameof(L3), () => "如果你开始听懂它在说什么......呃，恭喜，你已经迈出了疯狂的第一步");
            L4 = this.GetLocalization(nameof(L4), () => "不过别担心，疯狂也是一种清醒，只是角度不同而已");
        }
        protected override void Build() {
            DialogueBoxBase.RegisterPortrait(R1.Value, ADVAsset.HelenADV);
            DialogueBoxBase.SetPortraitStyle(R1.Value, silhouette: false);
            DialogueBoxBase.RegisterPortrait(R1.Value + naughty, ADVAsset.Helen_naughtyADV);
            DialogueBoxBase.SetPortraitStyle(R1.Value + naughty, silhouette: false);
            DialogueBoxBase.RegisterPortrait(R1.Value + solemn, ADVAsset.Helen_solemnADV);
            DialogueBoxBase.SetPortraitStyle(R1.Value + solemn, silhouette: false);
            DialogueBoxBase.RegisterPortrait(R1.Value + serious, ADVAsset.Helen_seriousADV);
            DialogueBoxBase.SetPortraitStyle(R1.Value + serious, silhouette: false);
            DialogueBoxBase.RegisterPortrait(R1.Value + enjoy, ADVAsset.Helen_enjoyADV);
            DialogueBoxBase.SetPortraitStyle(R1.Value + enjoy, silhouette: false);
            Add(R1.Value + solemn, L0.Value);
            Add(R1.Value + serious, L1.Value);
            Add(R1.Value, L2.Value); //奖励
            Add(R1.Value + naughty, L3.Value);
            Add(R1.Value + enjoy, L4.Value);
        }
        public override void PreProcessSegment(DialogueBoxBase.DialoguePreProcessArgs args) {
            if (args.Index == 2) {
                ADVRewardPopup.ShowReward(ModContent.ItemType<Brimlish>(), 1, null, appearDuration: 24, holdDuration: -1, giveDuration: 16, requireClick: true,
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
            return save.CalamitasCloneGift;
        }
        protected override void MarkGiftCompleted(ADVSave save) {
            save.CalamitasCloneGift = true;
        }
        protected override bool StartScenarioInternal() {
            return ScenarioManager.Start<CalamitasCloneGift>();
        }
    }
}

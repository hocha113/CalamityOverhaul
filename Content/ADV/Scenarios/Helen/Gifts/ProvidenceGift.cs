using CalamityMod.NPCs.Providence;
using CalamityOverhaul.Content.ADV.Common;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Helen.Gifts
{
    internal class ProvidenceGift : GiftScenarioBase
    {
        public override string Key => nameof(ProvidenceGift);
        public override int TargetBossID => ModContent.NPCType<Providence>();
        public static LocalizedText R1 { get; private set; }
        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }
        public static LocalizedText L4 { get; private set; }
        public override void SetStaticDefaults() {
            R1 = this.GetLocalization(nameof(R1), () => "比目鱼");
            L0 = this.GetLocalization(nameof(L0), () => "亵渎天神……一个由信仰和火焰构成的矛盾体。她守护的是什么？还是只是在燃烧？");
            L1 = this.GetLocalization(nameof(L1), () => "你刚才熄灭的不仅是圣火，还有一个纪元的余烬");
            L2 = this.GetLocalization(nameof(L2), () => "恶魔地狱鱼，从她的灰烬中重生的。它的温度永远保持在'刚好不会烫伤你'的程度");
            L3 = this.GetLocalization(nameof(L3), () => "这种精确控制让我怀疑，也许她只是想被理解");
            L4 = this.GetLocalization(nameof(L4), () => "不过理解和战争之间的界限，只是一次攻击的距离");
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
        public override void PreProcessSegment(DialogueBoxBase.DialoguePreProcessArgs args) {
            if (args.Index == 2) {
                ADVRewardPopup.ShowReward(ItemID.DemonicHellfish, 1, null, appearDuration: 24, holdDuration: -1, giveDuration: 16, requireClick: true,
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
            return save.ProvidenceGift;
        }
        protected override void MarkGiftCompleted(ADVSave save) {
            save.ProvidenceGift = true;
        }
        protected override bool StartScenarioInternal() {
            return ScenarioManager.Start<ProvidenceGift>();
        }
    }
}

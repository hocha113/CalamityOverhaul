using CalamityMod.NPCs.Crabulon;
using CalamityOverhaul.Content.ADV.Common;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Helen.Gifts
{
    internal class CrabulonGift : GiftScenarioBase
    {
        public override string Key => nameof(CrabulonGift);
        public override int TargetBossID => ModContent.NPCType<Crabulon>();
        public static LocalizedText R1 { get; private set; }
        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }
        public static LocalizedText L4 { get; private set; }
        public override void SetStaticDefaults() {
            R1 = this.GetLocalization(nameof(R1), () => "比目鱼");
            L0 = this.GetLocalization(nameof(L0), () => "那团蘑菇状的……生物？它的菌盖下藏着的究竟是智慧还是本能");
            L1 = this.GetLocalization(nameof(L1), () => "你知道吗，蘑菇的菌丝网络可以传递信息。也许它刚才在向同伴求救");
            L2 = this.GetLocalization(nameof(L2), () => "这是蘑菇鱼，从它身上散发出的孢子里提取的");
            L3 = this.GetLocalization(nameof(L3), () => "别担心，这些孢子不会让你变成菌类……大概");
            L4 = this.GetLocalization(nameof(L4), () => "不过如果你突然想在阴暗潮湿的地方扎根，记得告诉我");
        }
        protected override void Build() {
            DialogueBoxBase.RegisterPortrait(R1.Value, ADVAsset.HelenADV);
            DialogueBoxBase.RegisterPortrait(R1.Value + " ", ADVAsset.Helen_solemnADV);
            DialogueBoxBase.RegisterPortrait(R1.Value + " " + " ", ADVAsset.Helen_naughtyADV);
            Add(R1.Value + " ", L0.Value);
            Add(R1.Value + " ", L1.Value);
            Add(R1.Value, L2.Value); //奖励
            Add(R1.Value + " " + " ", L3.Value);
            Add(R1.Value + " " + " ", L4.Value);
        }
        public override void PreProcessSegment(DialogueBoxBase.DialoguePreProcessArgs args) {
            if (args.Index == 2) {
                ADVRewardPopup.ShowReward(ItemID.AmanitaFungifin, 1, null, appearDuration: 24, holdDuration: -1, giveDuration: 16, requireClick: true,
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
            return save.CrabulonGift;
        }
        protected override void MarkGiftCompleted(ADVSave save) {
            save.CrabulonGift = true;
        }
        protected override bool StartScenarioInternal() {
            return ScenarioManager.Start<CrabulonGift>();
        }
    }
}

using CalamityMod.NPCs.Yharon;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.ADV.Scenarios.Gifts
{
    internal class YharonGift : GiftScenarioBase
    {
        public override string Key => nameof(YharonGift);
        public override int TargetBossID => ModContent.NPCType<Yharon>();
        public static LocalizedText R1 { get; private set; }
        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }
        public static LocalizedText L4 { get; private set; }
        public override void SetStaticDefaults() {
            R1 = this.GetLocalization(nameof(R1), () => "比目鱼");
            L0 = this.GetLocalization(nameof(L0), () => "丛林龙，嗯......应该叫它焚世之龙，它燃烧的并非肉体，而是执念");
            L1 = this.GetLocalization(nameof(L1), () => "忠诚到愿意为主人燃尽自己，这种纯粹让我想起海底那些守护珊瑚礁的鱼群");
            L2 = this.GetLocalization(nameof(L2), () => "猩红虎鱼，从火焰的中心提取的。它的条纹像是被火焰烙印上去的誓言");
            L3 = this.GetLocalization(nameof(L3), () => "握着它会感觉到一种灼热的决心。那是属于战士的温度");
            L4 = this.GetLocalization(nameof(L4), () => "你击败了它，但我怀疑……它在倒下的瞬间，是否终于获得了解脱");
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
                ADVRewardPopup.ShowReward(ItemID.CrimsonTigerfish, 1, null, appearDuration: 24, holdDuration: -1, giveDuration: 16, requireClick: true,
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
            return save.YharonGift;
        }
        protected override void MarkGiftCompleted(ADVSave save) {
            save.YharonGift = true;
        }
        protected override bool StartScenarioInternal() {
            return ScenarioManager.Start<YharonGift>();
        }
    }
}

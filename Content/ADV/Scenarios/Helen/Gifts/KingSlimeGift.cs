using CalamityOverhaul.Content.ADV.Common;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Helen.Gifts
{
    internal class KingSlimeGift : GiftScenarioBase
    {
        public override string Key => nameof(KingSlimeGift);
        public override int TargetBossID => NPCID.KingSlime;
        public static LocalizedText R1 { get; private set; }
        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }
        public static LocalizedText L4 { get; private set; }
        public override void SetStaticDefaults() {
            R1 = this.GetLocalization(nameof(R1), () => "比目鱼");
            L0 = this.GetLocalization(nameof(L0), () => "刚才那团蓝色的……你称之为史莱姆？看起来更像是一种情绪的实体化");
            L1 = this.GetLocalization(nameof(L1), () => "或许是物质自我意识试图聚集和进化的一次拙劣尝试。可惜，它没有‘记忆’");
            L2 = this.GetLocalization(nameof(L2), () => "请拿好，这是史莱姆鱼。别挤它，它的心情会爆炸");
            L3 = this.GetLocalization(nameof(L3), () => "你有尝试过用这种生物去炖过汤吗?");
            L4 = this.GetLocalization(nameof(L4), () => "我是说......是的，在海底我们也可以炖汤，海底甚至也有海");
        }
        protected override void Build() {
            DialogueBoxBase.RegisterPortrait(R1.Value, ADVAsset.HelenADV);
            DialogueBoxBase.SetPortraitStyle(R1.Value, silhouette: false);
            DialogueBoxBase.RegisterPortrait(R1.Value + " ", ADVAsset.Helen_enjoyADV);
            DialogueBoxBase.SetPortraitStyle(R1.Value + " ", silhouette: false);
            DialogueBoxBase.RegisterPortrait(R1.Value + " " + " ", ADVAsset.Helen_solemnADV);
            DialogueBoxBase.SetPortraitStyle(R1.Value + " " + " ", silhouette: false);
            Add(R1.Value + " " + " ", L0.Value);
            Add(R1.Value + " " + " ", L1.Value);
            Add(R1.Value, L2.Value);//奖励
            Add(R1.Value + " ", L3.Value);
            Add(R1.Value + " ", L4.Value);
        }
        public override void PreProcessSegment(DialogueBoxBase.DialoguePreProcessArgs args) {
            if (args.Index == 2) {
                ADVRewardPopup.ShowReward(ItemID.Slimefish, 1, null, appearDuration: 24, holdDuration: -1, giveDuration: 16, requireClick: true,
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
            return save.KingSlimeGift;
        }
        protected override void MarkGiftCompleted(ADVSave save) {
            save.KingSlimeGift = true;
        }
        protected override bool StartScenarioInternal() {
            return ScenarioManager.Start<KingSlimeGift>();
        }
    }
}

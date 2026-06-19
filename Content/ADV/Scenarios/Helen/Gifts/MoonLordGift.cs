using CalamityOverhaul.Content.ADV.ADVRewardPopups;
using CalamityOverhaul.Content.ADV.Common;
using CalamityOverhaul.Content.ADV.DialogueBoxs;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Helen.Gifts
{
    internal class MoonLordGift : GiftScenarioBase
    {
        public override string Key => nameof(MoonLordGift);
        public override int TargetBossID => NPCID.MoonLordCore;
        public static LocalizedText R1 { get; private set; }
        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }
        public static LocalizedText L4 { get; private set; }
        private const string enjoy3 = " ";
        private const string enjoy = " " + " ";
        public override void SetStaticDefaults() {
            R1 = this.GetLocalization(nameof(R1), () => "比目鱼");
            L0 = this.GetLocalization(nameof(L0), () => "月球的主宰，哈！或者说，曾经是。现在它只是一堆漂浮的眼球和血肉");
            L1 = this.GetLocalization(nameof(L1), () => "我们刚才不仅拯救了世界，还关闭了一个从不应该被打开的门");
            L2 = this.GetLocalization(nameof(L2), () => "我逮到了云鱼，从天空的裂缝里飘下来的。它在手里像是没有重量");
            L3 = this.GetLocalization(nameof(L3), () => "据说它能让人看到未来……但那个未来已经被你改写了");
            L4 = this.GetLocalization(nameof(L4), () => "我们刚刚证明了，即使是神，也会流血");
        }
        protected override void Build() {
            DialogueBoxBase.RegisterPortrait(R1.Value, ADVAsset.HelenADV);
            DialogueBoxBase.SetPortraitStyle(R1.Value, silhouette: false);
            DialogueBoxBase.RegisterPortrait(R1.Value + enjoy3, ADVAsset.Helen_enjoy3ADV);
            DialogueBoxBase.SetPortraitStyle(R1.Value + enjoy3, silhouette: false);
            DialogueBoxBase.RegisterPortrait(R1.Value + enjoy, ADVAsset.Helen_enjoyADV);
            DialogueBoxBase.SetPortraitStyle(R1.Value + enjoy, silhouette: false);
            Add(R1.Value + enjoy3, L0.Value);
            Add(R1.Value, L1.Value);
            AddReward(R1.Value, L2.Value, ItemID.Cloudfish); //奖励
            Add(R1.Value, L3.Value);
            Add(R1.Value + enjoy, L4.Value);
        }
        protected override bool IsGiftCompleted(ADVSave save) {
            return save.Get<BossGiftADVData>().MoonLordGift;
        }
        protected override void MarkGiftCompleted(ADVSave save) {
            save.Get<BossGiftADVData>().MoonLordGift = true;
        }
        protected override bool StartScenarioInternal() {
            return ScenarioManager.Start<MoonLordGift>();
        }
    }
}

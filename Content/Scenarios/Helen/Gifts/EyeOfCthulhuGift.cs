using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Helen.Gifts
{
    internal sealed class EyeOfCthulhuGift : HelenBossGiftNarrative, ILocalizedModType
    {
        public string LocalizationCategory => "ADV";

        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }
        public static LocalizedText L4 { get; private set; }
        public static LocalizedText R1 { get; private set; }

        public override StyleId DefaultStyle => "Sea";

        public override int TargetBossId => NPCID.EyeofCthulhu;

        public override void SetStaticDefaults() {
            R1 = this.GetLocalization(nameof(R1), () => "比目鱼");
            L0 = this.GetLocalization(nameof(L0), () => "你的动作被那只巨眼拖成了慢镜头……我还以为你在刻意摆造型");
            L1 = this.GetLocalization(nameof(L1), () => "我们已经进入‘被注视的阶段’。这意味着更多麻烦");
            L2 = this.GetLocalization(nameof(L2), () => "我从眼睛的大嘴里找到了这个，拿着，这是克苏鲁鱼。它和同名的神话一样......不太讲逻辑");
            L3 = this.GetLocalization(nameof(L3), () => "小心收好，它会让你误以为自己开眼了。其实那不过是血液里多了点兴奋剂");
            L4 = this.GetLocalization(nameof(L4), () => "我很好奇你会不会开始对月亮眨眼");
        }

        protected override void Build(NarrativeComposer n) {
            n
             .Say("Helen", L0.Value)
             .Say("Helen", L1.Value)
             .SayReward("Helen", L2.Value, ItemID.TheFishofCthulu, title: string.Empty)
             .Say("Helen", L3.Value)
             .Say("Helen", "Naughty", L4.Value);
        }

        protected override bool IsGiftCompleted()
            => HalibutStorySync.ReadGift(d => d.EyeOfCthulhuGift, d => d.EyeOfCthulhuGift);

        protected override void MarkGiftCompleted()
            => HalibutStorySync.WriteGift(d => d.EyeOfCthulhuGift = true, d => d.EyeOfCthulhuGift = true);
    }
}

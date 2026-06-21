using CalamityOverhaul.Content.Narrative.Scenarios.Helen;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Narrative.Scenarios.Helen.Gifts
{
    internal sealed class MoonLordGift : HelenBossGiftNarrative, ILocalizedModType
    {
        public string LocalizationCategory => "ADV";

        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }
        public static LocalizedText L4 { get; private set; }
        public static LocalizedText R1 { get; private set; }

        public override StyleId DefaultStyle => "Sea";

        public override int TargetBossId => NPCID.MoonLordCore;

        public override void SetStaticDefaults() {
            R1 = this.GetLocalization(nameof(R1), () => "比目鱼");
            L0 = this.GetLocalization(nameof(L0), () => "月球的主宰，哈！或者说，曾经是。现在它只是一堆漂浮的眼球和血肉");
            L1 = this.GetLocalization(nameof(L1), () => "我们刚才不仅拯救了世界，还关闭了一个从不应该被打开的门");
            L2 = this.GetLocalization(nameof(L2), () => "我逮到了云鱼，从天空的裂缝里飘下来的。它在手里像是没有重量");
            L3 = this.GetLocalization(nameof(L3), () => "据说它能让人看到未来……但那个未来已经被你改写了");
            L4 = this.GetLocalization(nameof(L4), () => "我们刚刚证明了，即使是神，也会流血");
        }

        protected override void Build(NarrativeComposer n) {
            n
             .Say("Helen", "Enjoy3", L0.Value)
             .Say("Helen", L1.Value)
             .Reward(ItemID.Cloudfish, 1, string.Empty, blocking: false)
             .Say("Helen", L2.Value, onEnter: RewardLineAnchor)
             .Say("Helen", L3.Value)
             .Say("Helen", "Enjoy", L4.Value);
        }

        private static void RewardLineAnchor() { }

        protected override bool IsGiftCompleted()
            => HalibutStorySync.ReadGift(d => d.MoonLordGift, d => d.MoonLordGift);

        protected override void MarkGiftCompleted()
            => HalibutStorySync.WriteGift(d => d.MoonLordGift = true, d => d.MoonLordGift = true);
    }
}

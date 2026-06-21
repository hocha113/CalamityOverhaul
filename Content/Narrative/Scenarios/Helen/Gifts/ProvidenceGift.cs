using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Narrative.Scenarios.Helen;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Narrative.Scenarios.Helen.Gifts
{
    internal sealed class ProvidenceGift : HelenBossGiftNarrative, ILocalizedModType
    {
        public string LocalizationCategory => "ADV";

        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }
        public static LocalizedText L4 { get; private set; }
        public static LocalizedText R1 { get; private set; }

        public override StyleId DefaultStyle => "Sea";

        public override int TargetBossId => CWRID.NPC_Providence;

        public override void SetStaticDefaults() {
            R1 = this.GetLocalization(nameof(R1), () => "比目鱼");
            L0 = this.GetLocalization(nameof(L0), () => "亵渎天神……一个由信仰和火焰构成的矛盾体。挺可怜的");
            L1 = this.GetLocalization(nameof(L1), () => "我们刚才熄灭的不仅是圣火，还有一个纪元的余烬");
            L2 = this.GetLocalization(nameof(L2), () => "恶魔地狱鱼，从她的灰烬中重生的。它的温度永远保持在'刚好不会烫伤你'的程度");
            L3 = this.GetLocalization(nameof(L3), () => "这种精确控制让我怀疑，也许它只是想被理解");
            L4 = this.GetLocalization(nameof(L4), () => "不过理解和战争之间的界限，只是一次攻击的距离");
        }

        protected override void Build(NarrativeComposer n) {
            n
             .Say("Helen", L0.Value)
             .Say("Helen", L1.Value)
             .Reward(ItemID.DemonicHellfish, 1, string.Empty, blocking: false)
             .Say("Helen", L2.Value, onEnter: RewardLineAnchor)
             .Say("Helen", L3.Value)
             .Say("Helen", L4.Value);
        }

        private static void RewardLineAnchor() { }

        protected override bool IsGiftCompleted()
            => HalibutStorySync.ReadGift(d => d.ProvidenceGift, d => d.ProvidenceGift);

        protected override void MarkGiftCompleted()
            => HalibutStorySync.WriteGift(d => d.ProvidenceGift = true, d => d.ProvidenceGift = true);
    }
}

using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Helen.Gifts
{
    internal sealed class QueenBeeGift : HelenBossGiftNarrative, ILocalizedModType
    {
        public string LocalizationCategory => "ADV";

        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }
        public static LocalizedText R1 { get; private set; }
        public static LocalizedText R2 { get; private set; }

        public override StyleId DefaultStyle => "Sea";

        public override int TargetBossId => NPCID.QueenBee;

        public override void SetStaticDefaults() {
            R1 = this.GetLocalization(nameof(R1), () => "比目鱼");
            R2 = this.GetLocalization(nameof(R2), () => "???");
            L0 = this.GetLocalization(nameof(L0), () => "我差点以为脸要被埋进蜂蜜里了");
            L1 = this.GetLocalization(nameof(L1), () => "不过，我刚才从地上堆积的蜂蜜里摸到了一条鱼");
            L2 = this.GetLocalization(nameof(L2), () => "给，新鲜还热乎的蜂蜜鱼");
            L3 = this.GetLocalization(nameof(L3), () => "我觉得它非常适合做糖醋鲤鱼");
        }

        protected override void Build(NarrativeComposer n) {
            n
             .Say("Helen", "Enjoy", L0.Value)
             .Say("Helen", L1.Value)
             .SayReward("Helen", L2.Value, ItemID.Honeyfin, title: string.Empty)
             .Say("Helen", L3.Value);
        }

        protected override bool IsGiftCompleted()
            => HalibutStorySync.ReadGift(d => d.QueenBeeGift, d => d.QueenBeeGift);

        protected override void MarkGiftCompleted()
            => HalibutStorySync.WriteGift(d => d.QueenBeeGift = true, d => d.QueenBeeGift = true);
    }
}

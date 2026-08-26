using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Helen.Gifts
{
    internal sealed class SkeletronGift : HelenBossGiftNarrative, ILocalizedModType
    {
        public string LocalizationCategory => "ADV.Helen";

        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }
        public static LocalizedText L4 { get; private set; }

        public override StyleId DefaultStyle => "Sea";

        public override int TargetBossId => NPCID.SkeletronHead;

        public override void SetStaticDefaults() {
            L0 = this.GetLocalization(nameof(L0), () => "那可真是一大堆钙质!");
            L1 = this.GetLocalization(nameof(L1), () => "那东西的颅骨结构，让我想起一只失控的意念聚合体");
            L2 = this.GetLocalization(nameof(L2), () => "让我枪管冷却一下，我刚才从这周围捡到了一条鱼");
            L3 = this.GetLocalization(nameof(L3), () => "你看，这是‘骷髅王鱼’，据说它体内的磷质能让夜钓的人思考人生");
            L4 = this.GetLocalization(nameof(L4), () => "走吧，前面还有更抽象的骨头在等着我们");
        }

        protected override void Build(NarrativeComposer n) {
            n
             .Say("Helen", L0.Value)
             .Say("Helen", "Enjoy", L1.Value)
             .Say("Helen", "Enjoy", L2.Value)
             .SayReward("Helen", L3.Value, ItemID.Fishotron, title: string.Empty)
             .Say("Helen", L4.Value);
        }

        protected override bool IsGiftCompleted()
            => HalibutStorySync.ReadGift(d => d.SkeletronGift, d => d.SkeletronGift);

        protected override void MarkGiftCompleted()
            => HalibutStorySync.WriteGift(d => d.SkeletronGift = true, d => d.SkeletronGift = true);
    }
}

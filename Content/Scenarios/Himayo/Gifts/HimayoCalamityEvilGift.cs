using CalamityOverhaul.Content.Narrative;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Himayo.Gifts
{
    /// <summary>onikiri.002，A 跑题+碎嘴关心；非洁癖</summary>
    internal sealed class HimayoCalamityEvilGift : HimayoBossGiftNarrative, ILocalizedModType
    {
        public string LocalizationCategory => "ADV.Himayo";

        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }
        public static LocalizedText L4 { get; private set; }

        public override StyleId DefaultStyle => NarrativeIds.Onikiri;

        public override int[] TargetBossIds => [CWRID.NPC_HiveMind, CWRID.NPC_PerforatorHive];

        public override void SetStaticDefaults() {
            L0 = this.GetLocalization(nameof(L0), () => "先别站着发呆了。喘两口气也好啊");
            L1 = this.GetLocalization(nameof(L1), () => "刚才那摊抱成一团的……你有没有觉得，特别像坏掉的豆沙");
            L2 = this.GetLocalization(nameof(L2), () => "我一说出口就后悔。现在我自己也饿了");
            L3 = this.GetLocalization(nameof(L3), () => "你要是也饿，就去找点吃的。打完怪还硬撑着，最傻");
            L4 = this.GetLocalization(nameof(L4), () => "还有，手别往脸上抹。不是嫌你脏，抹完眼睛真的会辣，信我");
        }

        protected override void Build(NarrativeComposer n) {
            n.Say(NarrativeIds.Mayo, L0.Value, onEnter: PortraitFace(HimayoFullBodyPortrait.Face.Ruminate))
             .Say(NarrativeIds.Mayo, L1.Value, onEnter: PortraitFace(HimayoFullBodyPortrait.Face.Forsmile))
             .Say(NarrativeIds.Mayo, L2.Value)
             .Say(NarrativeIds.Mayo, L3.Value)
             .Say(NarrativeIds.Mayo, L4.Value);
        }

        protected override bool IsGiftCompleted()
            => HimayoStorySync.ReadGift(d => d.CalamityEvilGift, d => d.CalamityEvilGift);

        protected override void MarkGiftCompleted()
            => HimayoStorySync.WriteGift(d => d.CalamityEvilGift = true, d => d.CalamityEvilGift = true);
    }
}

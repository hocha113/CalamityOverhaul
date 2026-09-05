using CalamityOverhaul.Content.Narrative;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Himayo.Gifts
{
    /// <summary>onikiri.017，B 空/大</summary>
    internal sealed class HimayoDevourerOfGodsGift : HimayoBossGiftNarrative, ILocalizedModType
    {
        public string LocalizationCategory => "ADV.Himayo";

        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }

        public override StyleId DefaultStyle => NarrativeIds.Onikiri;

        public override void SetStaticDefaults() {
            L0 = this.GetLocalization(nameof(L0), () => "刚才那截身躯从虚空里碾过去的时候……真像把整片天空硬生生挖走了一大块");
            L1 = this.GetLocalization(nameof(L1), () => "看着底下那片深不见底的黑窟窿，我刚才都忍不住发了会儿愣");
            L2 = this.GetLocalization(nameof(L2), () => "回神啦，看你身上全须全尾的没缺胳膊少腿，我这心里才算踏实下来");
        }

        protected override void Build(NarrativeComposer n) {
            n.Say(NarrativeIds.Mayo, L0.Value, Voice[1], onEnter: PortraitFace(HimayoFullBodyPortrait.Face.Doubt))
             .Say(NarrativeIds.Mayo, L1.Value, Voice[2], onEnter: PortraitFace(HimayoFullBodyPortrait.Face.Ruminate))
             .GiftReward(GiftKey)
             .Say(NarrativeIds.Mayo, L2.Value, Voice[3], onEnter: PortraitFace(HimayoFullBodyPortrait.Face.Forsmile));
        }

        protected override bool IsGiftCompleted()
            => HimayoStorySync.ReadGift(d => d.DevourerOfGodsGift, d => d.DevourerOfGodsGift);

        protected override void MarkGiftCompleted()
            => HimayoStorySync.WriteGift(d => d.DevourerOfGodsGift = true, d => d.DevourerOfGodsGift = true);
    }
}

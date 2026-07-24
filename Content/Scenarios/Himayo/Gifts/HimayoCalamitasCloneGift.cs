using CalamityOverhaul.Content.Narrative;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Himayo.Gifts
{
    /// <summary>onikiri.010，E 烦模仿+塞物</summary>
    internal sealed class HimayoCalamitasCloneGift : HimayoBossGiftNarrative, ILocalizedModType
    {
        public string LocalizationCategory => "ADV.Himayo";

        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }

        public override StyleId DefaultStyle => NarrativeIds.Onikiri;
        public override int TargetBossId => CWRID.NPC_CalamitasClone;

        public override void SetStaticDefaults() {
            L0 = this.GetLocalization(nameof(L0), () => "学走路学得那么像，烦");
            L1 = this.GetLocalization(nameof(L1), () => "不是怕它，是看着别扭。学得越像越假");
            L2 = this.GetLocalization(nameof(L2), () => "拿着。问从哪摸的，我装听不见");
        }

        protected override void Build(NarrativeComposer n) {
            n.Say(NarrativeIds.Mayo, L0.Value, onEnter: PortraitFace(HimayoFullBodyPortrait.Face.Doubt))
             .Say(NarrativeIds.Mayo, L1.Value)
             .SayReward(NarrativeIds.Mayo, L2.Value, ItemID.IronPickaxe, title: string.Empty);
        }

        protected override bool IsGiftCompleted()
            => HimayoStorySync.ReadGift(d => d.CalamitasCloneGift, d => d.CalamitasCloneGift);

        protected override void MarkGiftCompleted()
            => HimayoStorySync.WriteGift(d => d.CalamitasCloneGift = true, d => d.CalamitasCloneGift = true);
    }
}

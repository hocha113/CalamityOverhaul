using CalamityOverhaul.Content.Scenarios.SupCal;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Helen.Gifts
{
    internal sealed class CalamitasCloneGift : HelenBossGiftNarrative, ILocalizedModType
    {
        public string LocalizationCategory => "ADV.Helen";

        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }
        public static LocalizedText L4 { get; private set; }

        public override StyleId DefaultStyle => "Sea";

        public override int TargetBossId => CWRID.NPC_CalamitasClone;

        public override void SetStaticDefaults() {
            L0 = this.GetLocalization(nameof(L0), () => "一个劣质的影子，让我想起来上大学时自己窝在海沟里捣鼓的基因怪兽，总之制作这个东西的家伙品味很差");
            L1 = this.GetLocalization(nameof(L1), () => "我有种不详的预感，真正的恐怖还在更深的地方等着，它甚至在靠近");
            L2 = this.GetLocalization(nameof(L2), () => "说回正事，硫磺火鱼，从灾厄的余烬中捡的。捧在手心里还能听到它在瞎嘀咕些什么");
            L3 = this.GetLocalization(nameof(L3), () => "如果你开始听懂它在说什么......呃，恭喜，你已经迈出了疯狂的第一步");
            L4 = this.GetLocalization(nameof(L4), () => "不过别担心，疯狂也是一种清醒，只是角度不同而已");
        }

        protected override void Build(NarrativeComposer n) {
            n
             .Say("Helen", "Solemn", L0.Value)
             .Say("Helen", "Stern", L1.Value)
             .SayReward("Helen", L2.Value, CWRID.Item_Brimlish, title: string.Empty)
             .Say("Helen", "Naughty", L3.Value)
             .Say("Helen", "Enjoy", L4.Value);
        }

        protected override bool IsGiftCompleted()
            => HalibutStorySync.ReadGift(d => d.CalamitasCloneGift, d => d.CalamitasCloneGift);

        protected override void MarkGiftCompleted()
            => HalibutStorySync.WriteGift(d => d.CalamitasCloneGift = true, d => d.CalamitasCloneGift = true);
        protected override bool AdditionalConditions(Player player)
            => !SupCalEffect.IsActive;
    }
}

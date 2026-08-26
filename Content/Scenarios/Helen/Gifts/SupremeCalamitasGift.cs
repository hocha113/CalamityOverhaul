using CalamityOverhaul.Content.Scenarios.SupCal.End.EternalBlazingNow;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Helen.Gifts
{
    internal sealed class SupremeCalamitasGift : HelenBossGiftNarrative, ILocalizedModType
    {
        public string LocalizationCategory => "ADV.Helen";

        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }
        public static LocalizedText L4 { get; private set; }

        public override StyleId DefaultStyle => "Sea";

        public override int TargetBossId => CWRID.NPC_SupremeCalamitas;

        protected override bool CanSpawned() => !EbnState.IsConquered(Main.LocalPlayer);

        public override void SetStaticDefaults() {
            L0 = this.GetLocalization(nameof(L0), () => "硫火女巫……一个复仇者，上世纪最强大的驭魔者");
            L1 = this.GetLocalization(nameof(L1), () => "我们刚才做的，是终结了一个传说，还是创造了一个新的开始？");
            L2 = this.GetLocalization(nameof(L2), () => "公主鱼，从灾厄余烬中诞生的。它带着一种矛盾的优雅，就像在废墟上盛开的花");
            L3 = this.GetLocalization(nameof(L3), () => "据说它能让人看到自己最想成为的样子，但那个样子往往是最不像自己的");
            L4 = this.GetLocalization(nameof(L4), () => "你现在逼近了作为泰拉人的力量顶点，接下来……就是漫长的下坡路了");
        }

        protected override void Build(NarrativeComposer n) {
            n
             .Say("Helen", L0.Value)
             .Say("Helen", "Enjoy", L1.Value)
             .SayReward("Helen", L2.Value, ItemID.PrincessFish, title: string.Empty)
             .Say("Helen", L3.Value)
             .Say("Helen", "Stern", L4.Value);
        }

        protected override bool IsGiftCompleted()
            => HalibutStorySync.ReadGift(d => d.SupremeCalamitasGift, d => d.SupremeCalamitasGift);

        protected override void MarkGiftCompleted()
            => HalibutStorySync.WriteGift(d => d.SupremeCalamitasGift = true, d => d.SupremeCalamitasGift = true);
    }
}

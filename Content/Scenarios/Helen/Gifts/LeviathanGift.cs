using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Helen.Gifts
{
    internal sealed class LeviathanGift : HelenBossGiftNarrative, ILocalizedModType
    {
        public string LocalizationCategory => "ADV.Helen";

        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }
        public static LocalizedText L4 { get; private set; }

        public override StyleId DefaultStyle => "Sea";

        public override int TargetBossId => CWRID.NPC_Leviathan;

        public override void SetStaticDefaults() {
            L0 = this.GetLocalization(nameof(L0), () => "海洋的暴君……和那个总是跟着她的小跟班。有些友谊超越了物种，也超越了理智");
            L1 = this.GetLocalization(nameof(L1), () => "我觉得最深的海沟里住着的不是恐惧，而是孤独。它们只是在寻找陪伴");
            L2 = this.GetLocalization(nameof(L2), () => "热带梭鱼，从深海漩涡里捞出来的。它看起来很普通，但这正是最可疑的地方");
            L3 = this.GetLocalization(nameof(L3), () => "越是平凡的外表，越是隐藏着不平凡的过去");
            L4 = this.GetLocalization(nameof(L4), () => "就像我们一样");
        }

        protected override void Build(NarrativeComposer n) {
            n
             .Say("Helen", L0.Value)
             .Say("Helen", "Enjoy", L1.Value)
             .SayReward("Helen", L2.Value, ItemID.TropicalBarracuda, title: string.Empty)
             .Say("Helen", L3.Value)
             .Say("Helen", L4.Value);
        }

        protected override bool IsGiftCompleted()
            => HalibutStorySync.ReadGift(d => d.LeviathanGift, d => d.LeviathanGift);

        protected override void MarkGiftCompleted()
            => HalibutStorySync.WriteGift(d => d.LeviathanGift = true, d => d.LeviathanGift = true);
    }
}

using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Helen.Gifts
{
    internal sealed class BrimstoneElementalGift : HelenBossGiftNarrative, ILocalizedModType
    {
        public string LocalizationCategory => "ADV";

        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }
        public static LocalizedText L4 { get; private set; }
        public static LocalizedText R1 { get; private set; }

        public override StyleId DefaultStyle => "Sea";

        public override int TargetBossId => CWRID.NPC_BrimstoneElemental;

        public override void SetStaticDefaults() {
            R1 = this.GetLocalization(nameof(R1), () => "比目鱼");
            L0 = this.GetLocalization(nameof(L0), () => "她往那里一站就像一场燃烧的演讲，幸好我们成功让她闭嘴了");
            L1 = this.GetLocalization(nameof(L1), () => "有些元素不是被创造的，而是从世界的裂缝中渗出来的古怪玩意儿");
            L2 = this.GetLocalization(nameof(L2), () => "黑曜石鱼，熔岩冷却的瞬间凝固产物。它的鳞片比地狱里纠缠的仇恨还要坚硬");
            L3 = this.GetLocalization(nameof(L3), () => "小心它在你手里自燃，情绪意义上的");
            L4 = this.GetLocalization(nameof(L4), () => "毕竟愤怒是会传染的");
        }

        protected override void Build(NarrativeComposer n) {
            n
             .Say("Helen", L0.Value)
             .Say("Helen", L1.Value)
             .SayReward("Helen", L2.Value, ItemID.Obsidifish, title: string.Empty)
             .Say("Helen", L3.Value)
             .Say("Helen", L4.Value);
        }

        protected override bool IsGiftCompleted()
            => HalibutStorySync.ReadGift(d => d.BrimstoneElementalGift, d => d.BrimstoneElementalGift);

        protected override void MarkGiftCompleted()
            => HalibutStorySync.WriteGift(d => d.BrimstoneElementalGift = true, d => d.BrimstoneElementalGift = true);
    }
}

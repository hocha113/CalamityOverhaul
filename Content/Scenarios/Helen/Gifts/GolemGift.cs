using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Helen.Gifts
{
    internal sealed class GolemGift : HelenBossGiftNarrative, ILocalizedModType
    {
        public string LocalizationCategory => "ADV.Helen";

        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }
        public static LocalizedText L4 { get; private set; }

        public override StyleId DefaultStyle => "Sea";

        public override int TargetBossId => NPCID.Golem;

        public override void SetStaticDefaults() {
            L0 = this.GetLocalization(nameof(L0), () => "一堆会动的石头，古代文明的遗产，或者说是他们失败的证明");
            L1 = this.GetLocalization(nameof(L1), () => "它可能在守护着什么，或者仅仅是在重复一个早已失去意义的程序");
            L2 = this.GetLocalization(nameof(L2), () => "岩鱼，从神殿的地基里挖出来的。它的密度高到让我怀疑人生");
            L3 = this.GetLocalization(nameof(L3), () => "我以前试着煮过它，但我的锅先投降了");
            L4 = this.GetLocalization(nameof(L4), () => "......有些东西存在的意义就是让人意识到，并非所有问题都需要被解决");
        }

        protected override void Build(NarrativeComposer n) {
            n
             .Say("Helen", "Solemn", L0.Value)
             .Say("Helen", "Solemn", L1.Value)
             .SayReward("Helen", L2.Value, ItemID.Rockfish, title: string.Empty)
             .Say("Helen", "Stern", L3.Value)
             .Say("Helen", "Stern", L4.Value);
        }

        protected override bool IsGiftCompleted()
            => HalibutStorySync.ReadGift(d => d.GolemGift, d => d.GolemGift);

        protected override void MarkGiftCompleted()
            => HalibutStorySync.WriteGift(d => d.GolemGift = true, d => d.GolemGift = true);
    }
}

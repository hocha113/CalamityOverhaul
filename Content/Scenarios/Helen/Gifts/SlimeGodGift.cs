using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Helen.Gifts
{
    internal sealed class SlimeGodGift : HelenBossGiftNarrative, ILocalizedModType
    {
        public string LocalizationCategory => "ADV";

        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }
        public static LocalizedText L4 { get; private set; }

        public override StyleId DefaultStyle => "Sea";

        public override int TargetBossId => CWRID.NPC_SlimeGodCore;

        public override void SetStaticDefaults() {
            L0 = this.GetLocalization(nameof(L0), () => "三个史莱姆，呃，或者说一个分裂的神性？这种二元对立的存在形式很有意思");
            L1 = this.GetLocalization(nameof(L1), () => "腐化与猩红，就像这个世界永恒的矛盾。而我们刚才证明了矛盾可以被'解决'");
            L2 = this.GetLocalization(nameof(L2), () => "这是杂色猪油鱼，从那堆粘液里捞出来的。别问我为什么叫这个名字");
            L3 = this.GetLocalization(nameof(L3), () => "它的颜色会随着观察者的心情改变，或许它在模仿人内心的混乱");
            L4 = this.GetLocalization(nameof(L4), () => "如果你盯着它看太久，可能会开始思考自己到底属于哪一边");
        }

        protected override void Build(NarrativeComposer n) {
            n
             .Say("Helen", L0.Value)
             .Say("Helen", L1.Value)
             .SayReward("Helen", L2.Value, ItemID.VariegatedLardfish, title: string.Empty)
             .Say("Helen", L3.Value)
             .Say("Helen", L4.Value);
        }

        protected override bool IsGiftCompleted()
            => HalibutStorySync.ReadGift(d => d.SlimeGodGift, d => d.SlimeGodGift);

        protected override void MarkGiftCompleted()
            => HalibutStorySync.WriteGift(d => d.SlimeGodGift = true, d => d.SlimeGodGift = true);
    }
}

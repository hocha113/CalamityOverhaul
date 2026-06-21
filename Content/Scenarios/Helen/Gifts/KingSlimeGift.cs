using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Helen.Gifts
{
    internal sealed class KingSlimeGift : HelenBossGiftNarrative, ILocalizedModType
    {
        public string LocalizationCategory => "ADV";

        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }
        public static LocalizedText L4 { get; private set; }

        public override StyleId DefaultStyle => "Sea";

        public override int TargetBossId => NPCID.KingSlime;

        public override void SetStaticDefaults() {
            L0 = this.GetLocalization(nameof(L0), () => "刚才那团蓝色的……你称之为史莱姆？看起来更像是一种固态的情绪");
            L1 = this.GetLocalization(nameof(L1), () => "或许是物质自我意识试图聚集和进化的一次拙劣尝试");
            L2 = this.GetLocalization(nameof(L2), () => "请拿好，这是史莱姆鱼。别挤它，它的心情会爆炸");
            L3 = this.GetLocalization(nameof(L3), () => "你有尝试过用这种生物去炖过汤吗?");
            L4 = this.GetLocalization(nameof(L4), () => "我是说......是的，在海底我们也可以炖汤，海底甚至也有海中的海");
        }

        protected override void Build(NarrativeComposer n) {
            n
             .Say("Helen", "Solemn", L0.Value)
             .Say("Helen", "Solemn", L1.Value)
             .SayReward("Helen", L2.Value, ItemID.Slimefish, title: string.Empty)
             .Say("Helen", "Enjoy", L3.Value)
             .Say("Helen", "Enjoy", L4.Value);
        }

        protected override bool IsGiftCompleted()
            => HalibutStorySync.ReadGift(d => d.KingSlimeGift, d => d.KingSlimeGift);

        protected override void MarkGiftCompleted()
            => HalibutStorySync.WriteGift(d => d.KingSlimeGift = true, d => d.KingSlimeGift = true);
    }
}

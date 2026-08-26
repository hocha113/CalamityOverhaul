using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Helen.Gifts
{
    internal sealed class YharonGift : HelenBossGiftNarrative, ILocalizedModType
    {
        public string LocalizationCategory => "ADV.Helen";

        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }
        public static LocalizedText L4 { get; private set; }

        public override StyleId DefaultStyle => "Sea";

        public override int TargetBossId => CWRID.NPC_Yharon;

        public override void SetStaticDefaults() {
            L0 = this.GetLocalization(nameof(L0), () => "丛林龙，嗯......应该叫它焚世之龙，它燃烧的并非肉体，而是执念");
            L1 = this.GetLocalization(nameof(L1), () => "忠诚到愿意为主人燃尽自己，这种纯粹让我想起海底那些守护珊瑚礁的鱼群");
            L2 = this.GetLocalization(nameof(L2), () => "猩红虎鱼，刚才逮到的，我很喜欢它身上的条纹");
            L3 = this.GetLocalization(nameof(L3), () => "握着它会感觉到一种灼热的决心，那是属于战士的温度");
            L4 = this.GetLocalization(nameof(L4), () => "你击败了那条龙，但我怀疑……它在倒下的瞬间，是否终于获得了解脱");
        }

        protected override void Build(NarrativeComposer n) {
            n
             .Say("Helen", "Enjoy", L0.Value)
             .Say("Helen", L1.Value)
             .SayReward("Helen", L2.Value, ItemID.CrimsonTigerfish, title: string.Empty)
             .Say("Helen", L3.Value)
             .Say("Helen", L4.Value);
        }

        protected override bool IsGiftCompleted()
            => HalibutStorySync.ReadGift(d => d.YharonGift, d => d.YharonGift);

        protected override void MarkGiftCompleted()
            => HalibutStorySync.WriteGift(d => d.YharonGift = true, d => d.YharonGift = true);
    }
}

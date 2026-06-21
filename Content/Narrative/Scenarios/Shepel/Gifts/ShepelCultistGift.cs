using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Frame;
using CalamityOverhaul.Content.Narrative.Scenarios.Shepel;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Narrative.Scenarios.Shepel.Gifts
{
    internal sealed class ShepelCultistGift : ShepelBossGiftNarrative, ILocalizedModType
    {
        public string LocalizationCategory => "ADV.Shepel";

        public static LocalizedText RoleName { get; private set; }
        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }

        public override StyleId DefaultStyle => "SHPC";

        public override int TargetBossId => NPCID.CultistBoss;

        public override void SetStaticDefaults() {
RoleName = this.GetLocalization(nameof(RoleName), () => "SHPC");
            L0 = this.GetLocalization(nameof(L0), () => "那些神神叨叨的信徒可算消停了，在这里搞些诡异的仪式。");
            L1 = this.GetLocalization(nameof(L1), () => "不过主人，您觉不觉得空气变得非常沉闷？我的传感器也在报警，好像有什么了不得的大麻烦正在从天上靠近。");
            L2 = this.GetLocalization(nameof(L2), () => "刚才我趁乱截获了那些乱七八糟的虚空信号，顺手把它改成了一个能帮您捕捉目标的小插件。");
            L3 = this.GetLocalization(nameof(L3), () => "看来接下来有一场硬仗要打，准备做好准备，主人，我随时都在。");
        }

        protected override void Build(NarrativeComposer n) {
            n
             .Say("SHPC", L0.Value)
             .Say("SHPC", L1.Value)
             .SayReward("SHPC", L2.Value, ModContent.ItemType<PhantomFrameModule>(), title: string.Empty)
             .Say("SHPC", L3.Value);
        }

        protected override bool IsGiftCompleted()
            => ShepelStorySync.ReadGift(d => d.CultistGift, d => d.CultistGift);

        protected override void MarkGiftCompleted()
            => ShepelStorySync.WriteGift(d => d.CultistGift = true, d => d.CultistGift = true);
    }
}

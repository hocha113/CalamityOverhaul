using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Barrel;
using CalamityOverhaul.Content.Narrative.Scenarios.Shepel;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Narrative.Scenarios.Shepel.Gifts
{
    internal sealed class ShepelCalamitasCloneGift : ShepelBossGiftNarrative, ILocalizedModType
    {
        public string LocalizationCategory => "ADV.Shepel";

        public static LocalizedText RoleName { get; private set; }
        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }

        public override StyleId DefaultStyle => "SHPC";

        public override int TargetBossId => CWRID.NPC_CalamitasClone;

        public override void SetStaticDefaults() {
RoleName = this.GetLocalization(nameof(RoleName), () => "SHPC");
            L0 = this.GetLocalization(nameof(L0), () => "那个克隆体终于消停了。虽然只是赝品，但她到处乱丢硫磺火球的坏习惯，还是挺让人头疼的。");
            L1 = this.GetLocalization(nameof(L1), () => "请您先退后几步，主人。地上这些残骸里还有一些火星在噼啪作响，这种危险的清扫工作交给我就好。");
            L2 = this.GetLocalization(nameof(L2), () => "趁着打扫的功夫，我把她那种魔法的追踪轨迹拆解了一下，塞进了您的光束导向模块里。");
            L3 = this.GetLocalization(nameof(L3), () => "有了它，以后您只需要站在最安全的距离外优雅地开火就好。");
        }

        protected override void Build(NarrativeComposer n) {
            n
             .Say("SHPC", L0.Value)
             .Say("SHPC", L1.Value)
             .SayReward("SHPC", L2.Value, ModContent.ItemType<ScorchBarrelModule>(), title: string.Empty)
             .Say("SHPC", L3.Value);
        }

        protected override bool IsGiftCompleted()
            => ShepelStorySync.ReadGift(d => d.CalamitasCloneGift, d => d.CalamitasCloneGift);

        protected override void MarkGiftCompleted()
            => ShepelStorySync.WriteGift(d => d.CalamitasCloneGift = true, d => d.CalamitasCloneGift = true);
    }
}

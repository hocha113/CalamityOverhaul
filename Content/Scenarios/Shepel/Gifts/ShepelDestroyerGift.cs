using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Stock;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Shepel.Gifts
{
    internal sealed class ShepelDestroyerGift : ShepelBossGiftNarrative, ILocalizedModType
    {
        public string LocalizationCategory => "ADV.Shepel";

        public static LocalizedText RoleName { get; private set; }
        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }

        public override StyleId DefaultStyle => "SHPC";

        public override int TargetBossId => NPCID.TheDestroyer;

        public override void SetStaticDefaults() {
            RoleName = this.GetLocalization(nameof(RoleName), () => "SHPC");
            L0 = this.GetLocalization(nameof(L0), () => "主人，这台巨型机械的残骸我已经扫描完毕了。说实话，它的设计逻辑真是粗暴得毫无美感。");
            L1 = this.GetLocalization(nameof(L1), () => "明明拥有那么庞大的能源，却只知道像没头苍蝇一样横冲直撞，完全是在浪费性能。");
            L2 = this.GetLocalization(nameof(L2), () => "我从它报废的数据流里提取了一套很有意思的冲击力处理方式。");
            L3 = this.GetLocalization(nameof(L3), () => "这样一来，开火时的后坐力会变得可以利用。");
        }

        protected override void Build(NarrativeComposer n) {
            n
             .Say("SHPC", L0.Value)
             .Say("SHPC", L1.Value)
             .SayReward("SHPC", L2.Value, ModContent.ItemType<RecoilStockModule>(), title: string.Empty)
             .Say("SHPC", L3.Value);
        }

        protected override bool IsGiftCompleted()
            => ShepelStorySync.ReadGift(d => d.DestroyerGift, d => d.DestroyerGift);

        protected override void MarkGiftCompleted()
            => ShepelStorySync.WriteGift(d => d.DestroyerGift = true, d => d.DestroyerGift = true);
    }
}

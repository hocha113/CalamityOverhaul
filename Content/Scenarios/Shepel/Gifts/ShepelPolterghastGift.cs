using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Frame;
using CalamityOverhaul.Content.Scenarios.Shepel;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Shepel.Gifts
{
    internal sealed class ShepelPolterghastGift : ShepelBossGiftNarrative, ILocalizedModType
    {
        public string LocalizationCategory => "ADV.Shepel";

        public static LocalizedText RoleName { get; private set; }
        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }

        public override StyleId DefaultStyle => "SHPC";

        public override int TargetBossId => CWRID.NPC_Polterghast;

        public override void SetStaticDefaults() {
RoleName = this.GetLocalization(nameof(RoleName), () => "SHPC");
            L0 = this.GetLocalization(nameof(L0), () => "地牢里的这些幽魂，一直无法解脱，真是可悲。");
            L1 = this.GetLocalization(nameof(L1), () => "这里的环境有些嘈杂，我已经为您开启了噪音过滤。");
            L2 = this.GetLocalization(nameof(L2), () => "我捕捉了那些游荡的灵体信号，将其改写成了能连续触发次级火力的程序。");
            L3 = this.GetLocalization(nameof(L3), () => "将这些无序的残影转化为您的火力，或许是它们最合理的归宿。");
        }

        protected override void Build(NarrativeComposer n) {
            n
             .Say("SHPC", L0.Value)
             .Say("SHPC", L1.Value)
             .SayReward("SHPC", L2.Value, ModContent.ItemType<RecursiveFrameModule>(), title: string.Empty)
             .Say("SHPC", L3.Value, onEnter: PortraitBlank);
        }

        protected override bool IsGiftCompleted()
            => ShepelStorySync.ReadGift(d => d.PolterghastGift, d => d.PolterghastGift);

        protected override void MarkGiftCompleted()
            => ShepelStorySync.WriteGift(d => d.PolterghastGift = true, d => d.PolterghastGift = true);
    }
}

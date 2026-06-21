using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Stock;
using CalamityOverhaul.Content.Scenarios.Shepel;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Shepel.Gifts
{
    internal sealed class ShepelEoWGift : ShepelBossGiftNarrative, ILocalizedModType
    {
        public string LocalizationCategory => "ADV.Shepel";

        public static LocalizedText RoleName { get; private set; }
        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }

        public override StyleId DefaultStyle => "SHPC";

        public override int TargetBossId => NPCID.EaterofWorldsHead;

        public override void SetStaticDefaults() {
RoleName = this.GetLocalization(nameof(RoleName), () => "SHPC");
            L0 = this.GetLocalization(nameof(L0), () => "腐化地的气味真是难闻。主人，您的装甲缝隙里沾到了一些可疑的黏液，让我为您清理干净。");
            L1 = this.GetLocalization(nameof(L1), () => "在这种泥泞的地方作战，弄脏衣服真是让人头疼。");
            L2 = this.GetLocalization(nameof(L2), () => "我把枪托的配重稍微调轻了一些。");
            L3 = this.GetLocalization(nameof(L3), () => "这样您使用时就不那么容易溅起地上的污泥了。");
        }

        protected override void Build(NarrativeComposer n) {
            n
             .Say("SHPC", L0.Value)
             .Say("SHPC", L1.Value)
             .SayReward("SHPC", L2.Value, ModContent.ItemType<LightStockModule>(), title: string.Empty)
             .Say("SHPC", L3.Value);
        }

        protected override bool IsGiftCompleted()
            => ShepelStorySync.ReadGift(d => d.EaterOfWorldsGift, d => d.EaterOfWorldsGift);

        protected override void MarkGiftCompleted()
            => ShepelStorySync.WriteGift(d => d.EaterOfWorldsGift = true, d => d.EaterOfWorldsGift = true);
    }
}

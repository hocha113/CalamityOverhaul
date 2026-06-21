using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Barrel;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Shepel.Gifts
{
    internal sealed class ShepelHiveMindGift : ShepelBossGiftNarrative, ILocalizedModType
    {
        public string LocalizationCategory => "ADV.Shepel";

        public static LocalizedText RoleName { get; private set; }
        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }

        public override StyleId DefaultStyle => "SHPC";

        public override int TargetBossId => CWRID.NPC_HiveMind;

        public override void SetStaticDefaults() {
            RoleName = this.GetLocalization(nameof(RoleName), () => "SHPC");
            L0 = this.GetLocalization(nameof(L0), () => "那个大脑散发的波动，带有强烈的精神干扰。主人，您的脑波频率刚才有些异常。");
            L1 = this.GetLocalization(nameof(L1), () => "请深呼吸，将注意力集中在我的系统提示音上，屏蔽掉那些亵渎的低语。");
            L2 = this.GetLocalization(nameof(L2), () => "我把那种干扰波动进行了反向编译。现在，您的攻击也能附带撕裂敌方护盾的高频震荡。");
            L3 = this.GetLocalization(nameof(L3), () => "危机已解除。回去之后，请允许我为您泡一杯安神的茶。");
        }

        protected override void Build(NarrativeComposer n) {
            n
             .Say("SHPC", L0.Value)
             .Say("SHPC", L1.Value)
             .SayReward("SHPC", L2.Value, ModContent.ItemType<OscillatorBarrelModule>(), title: string.Empty)
             .Say("SHPC", L3.Value);
        }

        protected override bool IsGiftCompleted()
            => ShepelStorySync.ReadGift(d => d.HiveMindGift, d => d.HiveMindGift);

        protected override void MarkGiftCompleted()
            => ShepelStorySync.WriteGift(d => d.HiveMindGift = true, d => d.HiveMindGift = true);
    }
}

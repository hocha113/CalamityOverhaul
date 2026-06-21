using CalamityOverhaul.Content.LegendWeapon.HalibutLegend;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.SupCal
{
    /// <summary>选择迎战但被至尊灾厄击败后的场景</summary>
    internal sealed class SupCalPlayerDefeat : NarrativeScenario, ILocalizedModType
    {
        public string LocalizationCategory => "ADV";
        public static LocalizedText Line1 { get; private set; }
        public static LocalizedText Line2 { get; private set; }
        public static LocalizedText Line3 { get; private set; }
        public static LocalizedText Line4 { get; private set; }
        public static LocalizedText Line5 { get; private set; }
        public static LocalizedText Line6 { get; private set; }
        public static LocalizedText Line7 { get; private set; }
        public static LocalizedText Line8 { get; private set; }
        public static LocalizedText Line9 { get; private set; }

        public override StyleId DefaultStyle => "Brimstone";

        public override void SetStaticDefaults() {

            Line1 = this.GetLocalization(nameof(Line1), () => "一碰就碎，你是纸做的吗");
            Line2 = this.GetLocalization(nameof(Line2), () => "我以为遇到了一个耐烧点的玩具");
            Line3 = this.GetLocalization(nameof(Line3), () => "高估你了，你现在的器量，连让我热身都不配");
            Line4 = this.GetLocalization(nameof(Line4), () => "等你真正强大到能被我正视时，再来吧");
            Line5 = this.GetLocalization(nameof(Line5), () => "不过...你确实有几分胆魄");
            Line6 = this.GetLocalization(nameof(Line6), () => "敢于直视我，甚至对我拔剑的凡人...你是第一个");
            Line7 = this.GetLocalization(nameof(Line7), () => "(在火焰纷飞中消失)");
            Line8 = this.GetLocalization(nameof(Line8), () => "如果我不顾后果睁开到第九只眼，应该有胜算，大不了......");
            Line9 = this.GetLocalization(nameof(Line9), () => "唉......");
        }

        protected override void Build(NarrativeComposer n) {
            n.Say("SupCal", "Despise", Line1.Value)
             .Say("SupCal", "Despise", Line2.Value)
             .Say("SupCal", Line3.Value)
             .Say("SupCal", Line4.Value)
             .Say("SupCal", "CloseEye", Line5.Value)
             .Say("SupCal", "CloseEye", Line6.Value)
             .Say("SupCal", Line7.Value);

            if (HasHalibut()) {
                n.Say("Helen", "Solemn", Line8.Value)
                 .Say("Helen", "Solemn", Line9.Value);
            }
        }

        private static bool HasHalibut() {
            try {
                return Main.LocalPlayer.TryGetOverride(out HalibutPlayer halibutPlayer) && halibutPlayer.HasHalubut;
            } catch {
                return false;
            }
        }
    }
}

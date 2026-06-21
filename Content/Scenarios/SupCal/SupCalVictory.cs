using CalamityOverhaul.Content.LegendWeapon.HalibutLegend;
using CalamityOverhaul.Content.Scenarios.Helen;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.SupCal
{
    /// <summary>迎战并击败至尊灾厄</summary>
    internal sealed class SupCalVictory : NarrativeScenario, ILocalizedModType
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

        public override StyleId DefaultStyle => "Brimstone";

        public override void SetStaticDefaults() {

            Line1 = this.GetLocalization(nameof(Line1), () => "什么......怎么可能!");
            Line2 = this.GetLocalization(nameof(Line2), () => "你竟然......打败了我?!");
            Line3 = this.GetLocalization(nameof(Line3), () => "不可能，这绝不可能......");
            Line4 = this.GetLocalization(nameof(Line4), () => "我可是已经超越了泰拉人的极限......");
            Line5 = this.GetLocalization(nameof(Line5), () => "......看来我确实小看你了");
            Line6 = this.GetLocalization(nameof(Line6), () => "但这不代表结束，我会回来的");
            Line7 = this.GetLocalization(nameof(Line7), () => "下次......下次我不会再大意了！");
            Line8 = this.GetLocalization(nameof(Line8), () => "......她逃走了");
        }

        protected override void Build(NarrativeComposer n) {
            n.Say("SupCal", "Shock", Line1.Value)
             .Say("SupCal", "Shock", Line2.Value)
             .Say("SupCal", "CloseEye", Line3.Value)
             .Say("SupCal", "CloseEye", Line4.Value)
             .Say("SupCal", "CloseEye", Line5.Value)
             .Say("SupCal", Line6.Value);

            if (HasHalibut()) {
                n.Say("SupCal", Line7.Value)
                 .Say("Helen", "Solemn", Line8.Value);
            }
            else {
                n.Say("SupCal", Line7.Value);
            }

            n.Reward(CWRID.Item_AshesofCalamity, 999, string.Empty);
        }

        protected override void OnCompleted() {
            HalibutStorySync.WriteSupCal(
                d => d.SupCalDefeat = true,
                d => d.SupCalDefeat = true);
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

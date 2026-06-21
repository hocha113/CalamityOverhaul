using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.LegendWeapon.HalibutLegend;
using CalamityOverhaul.Content.Scenarios.Helen;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.SupCal.Quest.PallbearerQuest
{
    internal sealed class SupCalMoonLordReward : NarrativeScenario, ILocalizedModType
    {
        public string LocalizationCategory => "ADV";
        public static LocalizedText Line1 { get; private set; }
        public static LocalizedText Line2 { get; private set; }
        public static LocalizedText Line3 { get; private set; }
        public static LocalizedText Line4 { get; private set; }
        public static LocalizedText Line5 { get; private set; }
        public static LocalizedText Line6 { get; private set; }
        public static LocalizedText Line7 { get; private set; }

        public override StyleId DefaultStyle => "Brimstone";

        public override void SetStaticDefaults() {

            Line1 = this.GetLocalization(nameof(Line1), () => "呵呵呵");
            Line2 = this.GetLocalization(nameof(Line2), () => "那个家伙……竟已落到这种地步");
            Line3 = this.GetLocalization(nameof(Line3), () => "你知道现在的地底是什么景象吗?");
            Line4 = this.GetLocalization(nameof(Line4), () => "......所以你这次来是?");
            Line5 = this.GetLocalization(nameof(Line5), () => "送你点小玩具，顺带有个委托交给你");
            Line6 = this.GetLocalization(nameof(Line6), () => "一把小巧的弩，我需要你拿它干掉下面的那个苟延残喘吸食地热的家伙，记住只能用这个弩");
            Line7 = this.GetLocalization(nameof(Line7), () => "如果你做到了，我们的合作还能继续");
        }

        protected override void Build(NarrativeComposer n) {
            n.Say("SupCal", Line1.Value)
             .Say("SupCal", Line2.Value)
             .Say("SupCal", Line3.Value);

            if (HasHalibut()) {
                n.Say("Helen", "Solemn", Line4.Value);
            }

            n.Say("SupCal", Line5.Value)
             .Say("SupCal", Line6.Value)
             .Reward(ModContent.ItemType<Pallbearer>(), 1, string.Empty)
             .Say("SupCal", Line7.Value);
        }

        protected override void OnStarted() => SupCalEffect.IsActive = true;

        protected override void OnCompleted() {
            SupCalEffect.IsActive = false;
            HalibutStorySync.WriteSupCal(
                d => d.SupCalMoonLordReward = true,
                d => d.SupCalMoonLordReward = true);
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

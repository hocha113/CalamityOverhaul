using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using InnoVault.Narrative.Runtime;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Draedon.Defeats
{
    internal sealed class ExoMechQuickDefeat : NarrativeScenario, ILocalizedModType
    {
        public string LocalizationCategory => "ADV.Draedon";
        public static LocalizedText QuickDefeatLine1 { get; private set; }
        public static LocalizedText QuickDefeatLine2 { get; private set; }
        public static LocalizedText QuickDefeatLine3 { get; private set; }

        public override StyleId DefaultStyle => "Draedon";

        public override void SetStaticDefaults() {
            QuickDefeatLine1 = this.GetLocalization(nameof(QuickDefeatLine1), () => "......这个时间远低于我的预测模型");
            QuickDefeatLine2 = this.GetLocalization(nameof(QuickDefeatLine2), () => "看来我低估了你当前的战斗效率");
            QuickDefeatLine3 = this.GetLocalization(nameof(QuickDefeatLine3), () => "或许是时候考虑更激进的设计方案了");
        }

        protected override void Build(NarrativeComposer n) {
            n
             .Say("Draedon", QuickDefeatLine1.Value)
             .Say("Draedon", QuickDefeatLine2.Value)
             .Say("Draedon", QuickDefeatLine3.Value);
        }

        protected override void OnStarted() {
            DraedonEffect.IsActive = true;
            DraedonEffect.Send();
        }

        protected override void OnCompleted() {
            DraedonEffect.IsActive = false;
            DraedonEffect.Send();
        }

        protected override NarrativePolicy ConfigurePolicy() => new() {
            CanTrigger = (_, _) => false,
        };

    }
}

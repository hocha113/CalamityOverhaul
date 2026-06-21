using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using InnoVault.Narrative.Runtime;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Narrative.Scenarios.Draedon.Defeats
{
    internal sealed class ExoMechHardDefeat : NarrativeScenario, ILocalizedModType
    {
        public string LocalizationCategory => "ADV";

        public static LocalizedText DraedonName { get; private set; }
        public static LocalizedText HardDefeatLine1 { get; private set; }
        public static LocalizedText HardDefeatLine2 { get; private set; }

        public override StyleId DefaultStyle => "Draedon";

        public override void SetStaticDefaults() {
            DraedonName = this.GetLocalization(nameof(DraedonName), () => "嘉登");

            //艰难战败对话：表现出对接近极限的认可
            HardDefeatLine1 = this.GetLocalization(nameof(HardDefeatLine1), () => "极限状态下的决策，这才是我想看到的数据");
            HardDefeatLine2 = this.GetLocalization(nameof(HardDefeatLine2), () => "在压力之下你仍能保持理性，令人满意");
        }

        protected override void Build(NarrativeComposer n) {
            n
             .Say("Draedon", HardDefeatLine1.Value)
             .Say("Draedon", HardDefeatLine2.Value);
        }

        private static void RewardLineAnchor() { }

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

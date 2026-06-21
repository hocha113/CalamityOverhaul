using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using InnoVault.Narrative.Runtime;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Draedon.Defeats
{
    internal sealed class ExoMechThirdDefeat : NarrativeScenario, ILocalizedModType
    {
        public string LocalizationCategory => "ADV";

        public static LocalizedText DraedonName { get; private set; }
        public static LocalizedText ThirdDefeatLine1 { get; private set; }
        public static LocalizedText ThirdDefeatLine2 { get; private set; }

        public override StyleId DefaultStyle => "Draedon";

        public override void SetStaticDefaults() {
            DraedonName = this.GetLocalization(nameof(DraedonName), () => "嘉登");

            //第三次战败对话：表现出对玩家一贯性的肯定
            ThirdDefeatLine1 = this.GetLocalization(nameof(ThirdDefeatLine1), () => "稳定的表现，这正是我所追求的完美的一部分");
            ThirdDefeatLine2 = this.GetLocalization(nameof(ThirdDefeatLine2), () => "你已经证明了自己的价值不是偶然");
        }

        protected override void Build(NarrativeComposer n) {
            n
             .Say("Draedon", ThirdDefeatLine1.Value)
             .Say("Draedon", ThirdDefeatLine2.Value);
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
            IsCompleted = _ => DraedonStorySync.ReadDraedon(d => d.ExoMechThirdDefeat, d => d.ExoMechThirdDefeat),
            CanTrigger = (_, _) => false,
            OnCompleted = _ => DraedonStorySync.WriteDraedon(d => d.ExoMechThirdDefeat = true, d => d.ExoMechThirdDefeat = true),
        };

    }
}

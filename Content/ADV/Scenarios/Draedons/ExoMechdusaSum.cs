using System;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Draedons
{
    internal class ExoMechdusaSum : ADVScenarioBase, ILocalizedModType, IWorldInfo
    {
        public string LocalizationCategory => "ADV";

        public override string Key => nameof(ExoMechdusaSum);

        protected override Func<DialogueBoxBase> DefaultDialogueStyle => () => DraedonDialogueBox.Instance;

        protected override void Build() {
            Add("嘉登", ".......你的行为没什么必要");
        }
    }
}

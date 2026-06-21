using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using InnoVault.Narrative.Runtime;
using Terraria.Localization;
using Terraria.ModLoader;
using CalamityOverhaul.Content.Narrative.Scenarios.Draedon;

namespace CalamityOverhaul.Content.Narrative.Scenarios.Draedon.Defeats
{
    internal sealed class ExoMechSecondDefeat : NarrativeScenario, ILocalizedModType
    {
        public string LocalizationCategory => "ADV";

        public static LocalizedText DraedonName { get; private set; }
        public static LocalizedText SecondDefeatLine1 { get; private set; }
        public static LocalizedText SecondDefeatLine2 { get; private set; }
        public static LocalizedText SecondDefeatLine3 { get; private set; }

        public override StyleId DefaultStyle => "Draedon";

        public override void SetStaticDefaults() {
DraedonName = this.GetLocalization(nameof(DraedonName), () => "嘉登");

            //第二次战败对话：表现出对玩家学习能力的认可
            SecondDefeatLine1 = this.GetLocalization(nameof(SecondDefeatLine1), () => "有趣，你的适应速度超出了我的预期");
            SecondDefeatLine2 = this.GetLocalization(nameof(SecondDefeatLine2), () => "数据显示你在上次战斗后已经进行了针对性的改进");
            SecondDefeatLine3 = this.GetLocalization(nameof(SecondDefeatLine3), () => "看来我需要重新评估你的学习曲线了");
        }

        protected override void Build(NarrativeComposer n) {
            n
             .Say("Draedon", SecondDefeatLine1.Value)
             .Say("Draedon", SecondDefeatLine2.Value)
             .Say("Draedon", SecondDefeatLine3.Value);
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
            IsCompleted = _ => DraedonStorySync.ReadDraedon(d => d.ExoMechSecondDefeat, d => d.ExoMechSecondDefeat),
            CanTrigger = (_, _) => false,
            OnCompleted = _ => DraedonStorySync.WriteDraedon(d => d.ExoMechSecondDefeat = true, d => d.ExoMechSecondDefeat = true),
        };

    }
}

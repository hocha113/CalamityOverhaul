using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Draedon.Quest.DeploySignaltowers
{
    internal sealed class FirstTowerBuiltScenario : NarrativeScenario, ILocalizedModType
    {
        public string LocalizationCategory => "ADV";

        public static LocalizedText DraedonName { get; private set; }
        public static LocalizedText Line1 { get; private set; }
        public static LocalizedText Line2 { get; private set; }
        public static LocalizedText Line3 { get; private set; }
        public static LocalizedText Line4 { get; private set; }

        public override StyleId DefaultStyle => "Draedon";

        public override void SetStaticDefaults() {
            DraedonName = this.GetLocalization(nameof(DraedonName), () => "嘉登");

            Line1 = this.GetLocalization(nameof(Line1), () => "新的量子纠缠节点运行平稳，信号稳定");
            Line2 = this.GetLocalization(nameof(Line2), () => "很好，这是网络的第一步。继续部署剩余的信号塔");
            Line3 = this.GetLocalization(nameof(Line3), () => "当节点数量达到标准时，我将能够对更大范围进行精确观测");
            Line4 = this.GetLocalization(nameof(Line4), () => "继续保持你的效率");
        }

        protected override void Build(NarrativeComposer n) {
            n
             .Say("Draedon", "Red", Line1.Value)
             .Say("Draedon", Line2.Value)
             .Say("Draedon", Line3.Value)
             .Say("Draedon", Line4.Value);
        }

        protected override void OnStarted() {
            DraedonEffect.IsActive = true;
            DraedonEffect.Send();
        }

        protected override void OnCompleted() {
            DraedonEffect.IsActive = false;
            DraedonEffect.Send();
        }

    }
}

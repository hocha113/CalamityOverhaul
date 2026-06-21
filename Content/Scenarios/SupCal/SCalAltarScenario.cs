using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.SupCal
{
    internal sealed class SCalAltarScenario : NarrativeScenario, ILocalizedModType
    {
        public static int Count = -1;

        public string LocalizationCategory => "ADV";

        public static LocalizedText Rolename1 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }

        public override StyleId DefaultStyle => "Brimstone";

        public override void SetStaticDefaults() {
            Rolename1 = this.GetLocalization(nameof(Rolename1), () => "硫火女巫");
            L1 = this.GetLocalization(nameof(L1), () => "现在还不是时候，你的前方还有另一个挡路的敌人");
            L2 = this.GetLocalization(nameof(L2), () => "去把你那堆机械玩具拼好，再把他打倒");
            L3 = this.GetLocalization(nameof(L3), () => "……怎么？需要我再说一遍吗？");
        }

        protected override void Build(NarrativeComposer n) {
            string line = L1.Value;
            if (Count == 1) {
                line = L2.Value;
            }
            if (Count == 2) {
                line = L3.Value;
            }

            n.Say("SupCal", "BeTo", line);
        }

        public static void ResetWorldState() => Count = -1;
    }
}

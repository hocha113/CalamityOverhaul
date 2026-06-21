using CalamityOverhaul.Content.Scenarios.OldDuke;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using InnoVault.Narrative.Runtime;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.OldDuke.Quest.FindFragments
{
    internal sealed class FirstCampsiteDialogue : NarrativeScenario, ILocalizedModType
    {
        public string LocalizationCategory => "ADV";

        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }
        public static LocalizedText L4 { get; private set; }
        public static LocalizedText L5 { get; private set; }
        public static LocalizedText L6 { get; private set; }

        public override StyleId DefaultStyle => "Sulfsea";

        public override void SetStaticDefaults() {
            L0 = this.GetLocalization(nameof(L0), () => "你来了");
            L1 = this.GetLocalization(nameof(L1), () => "我收集这些残片，已经有些年头了");
            L2 = this.GetLocalization(nameof(L2), () => "它们很……诡异。不像是自然演化的产物，倒像是某种“入侵”进这个世界的异物");
            L3 = this.GetLocalization(nameof(L3), () => "我希望你能明白我的意思，我的直觉告诉我它们本应是无形之物，只是选择了某种可以被泰拉生物理解的形态");
            L4 = this.GetLocalization(nameof(L4), () => "总之...我想要解读它们，但数量还不够");
            L5 = this.GetLocalization(nameof(L5), () => "我希望你能帮我收集足够多的海洋残片");
            L6 = this.GetLocalization(nameof(L6), () => "我将给予你相应的报酬");
        }

        protected override void Build(NarrativeComposer n) {
            n.Say("OldDuke", L0.Value)
             .Say("OldDuke", L1.Value)
             .Say("OldDuke", L2.Value)
             .Say("OldDuke", L3.Value)
             .Say("OldDuke", L4.Value)
             .Say("OldDuke", L5.Value)
             .Say("OldDuke", L6.Value)
             .End();
        }

        protected override NarrativePolicy ConfigurePolicy() => new() {
            IsCompleted = _ => OldDukeStorySync.Read(
                d => d.OldDukeFirstCampsiteDialogueCompleted || d.OldDukeFindFragmentsQuestTriggered,
                d => d.OldDukeFirstCampsiteDialogueCompleted || d.OldDukeFindFragmentsQuestTriggered),
            CanTrigger = (_, _) => false,
            OnCompleted = _ => OldDukeStorySync.Write(
                d => d.OldDukeFindFragmentsQuestTriggered = true,
                d => d.OldDukeFindFragmentsQuestTriggered = true),
        };
    }
}

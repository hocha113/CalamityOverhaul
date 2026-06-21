using CalamityOverhaul.Content.Scenarios.Draedon;
using CalamityOverhaul.OtherMods.InfernumMode;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using InnoVault.Narrative.Runtime;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Draedon.ExoMechdusaSums
{
    internal sealed class ExoMechdusaSum : NarrativeScenario, ILocalizedModType
    {
        public string LocalizationCategory => "ADV";

        public static LocalizedText DraedonName { get; private set; }
        public static LocalizedText IntroLine1 { get; private set; }
        public static LocalizedText IntroLine2 { get; private set; }
        public static LocalizedText IntroLine3 { get; private set; }
        public static LocalizedText IntroLine4 { get; private set; }
        public static LocalizedText IntroLine5 { get; private set; }
        public static LocalizedText SelectionPrompt { get; private set; }
        public static LocalizedText ChoiceAres { get; private set; }
        public static LocalizedText ChoiceThanatos { get; private set; }
        public static LocalizedText ChoiceTwins { get; private set; }
        public static LocalizedText BossRushLine { get; private set; }

        public static bool SimpleMode;

        public static bool CompatibleMode {
            get {
                if (CWRMod.Instance.fargowiltasCrossmod != null) {
                    return true;
                }
                if (CWRMod.Instance.infernum != null && InfernumRef.InfernumModeOpenState) {
                    return true;
                }
                if (CWRMod.Instance.woTM != null) {
                    return true;
                }
                return false;
            }
        }

        private const float TimeLimitSeconds = 20f;

        public override StyleId DefaultStyle => "Draedon";

        public override void SetStaticDefaults() {
            DraedonName = this.GetLocalization(nameof(DraedonName), () => "嘉登");
            IntroLine1 = this.GetLocalization(nameof(IntroLine1), () => "你知道吗？这一刻我已经等了太久了");
            IntroLine2 = this.GetLocalization(nameof(IntroLine2), () => "我对一切未知感到着迷，但最让我着迷的莫过于你的本质");
            IntroLine3 = this.GetLocalization(nameof(IntroLine3), () => "我将会向你展示，我那些超越神明的造物");
            IntroLine4 = this.GetLocalization(nameof(IntroLine4), () => "而你，则将在战斗中向我展示你的本质");
            IntroLine5 = this.GetLocalization(nameof(IntroLine5), () => "现在，选择吧");
            SelectionPrompt = this.GetLocalization(nameof(SelectionPrompt), () => "做出你的选择");
            BossRushLine = this.GetLocalization(nameof(BossRushLine), () => "做出你的选择。你有20秒的时间");
            ChoiceAres = this.GetLocalization(nameof(ChoiceAres), () => "战神阿瑞斯");
            ChoiceThanatos = this.GetLocalization(nameof(ChoiceThanatos), () => "死神塔纳托斯");
            ChoiceTwins = this.GetLocalization(nameof(ChoiceTwins), () => "双子神阿尔忒弥斯");
        }

        protected override void Build(NarrativeComposer n) {
            if (DraedonStorySync.ReadDraedon(d => d.FirstExoMechdusaSum, d => d.FirstExoMechdusaSum)) {
                SimpleMode = true;
            }

            bool simpleMode = CWRRef.GetBossRushActive() || SimpleMode;

            if (simpleMode) {
                if (CompatibleMode) {
                    n.Say("Draedon", "Red", BossRushLine.Value, onExit: EnableVanillaSelect);
                }
                else {
                    n.Choice("Draedon", "Red", BossRushLine.Value, c => c
                        .Timed(TimeLimitSeconds)
                        .Option("ares", ChoiceAres.Value, onSelect: () => SummonMech(ExoMechType.Prime))
                        .Option("thanatos", ChoiceThanatos.Value, onSelect: () => SummonMech(ExoMechType.Destroyer))
                        .Option("twins", ChoiceTwins.Value, onSelect: () => SummonMech(ExoMechType.Twins)));
                }
            }
            else {
                n.Say("Draedon", IntroLine1.Value)
                 .Say("Draedon", IntroLine2.Value)
                 .Say("Draedon", IntroLine3.Value)
                 .Say("Draedon", "Red", IntroLine4.Value);

                if (CompatibleMode) {
                    n.Say("Draedon", "Red", IntroLine5.Value, onExit: EnableVanillaSelect);
                }
                else {
                    n.Choice("Draedon", "Red", IntroLine5.Value, c => c
                        .Option("ares", ChoiceAres.Value, onSelect: () => SummonMech(ExoMechType.Prime))
                        .Option("thanatos", ChoiceThanatos.Value, onSelect: () => SummonMech(ExoMechType.Destroyer))
                        .Option("twins", ChoiceTwins.Value, onSelect: () => SummonMech(ExoMechType.Twins)));
                }
            }
        }

        protected override void OnStarted() {
            DraedonEffect.IsActive = true;
            DraedonEffect.Send();
            if (!CompatibleMode) {
                ExoMechdusaSumRender.RegisterHoverEffects();
            }
        }

        protected override void OnCompleted() {
            DraedonStorySync.WriteDraedon(
                d => d.FirstExoMechdusaSum = true,
                d => d.FirstExoMechdusaSum = true);
            SimpleMode = false;
            DraedonEffect.IsActive = false;
            DraedonEffect.Send();
            if (!CompatibleMode) {
                ExoMechdusaSumRender.Cleanup();
            }
        }

        private static void EnableVanillaSelect() {
            CWRRef.SetAbleToSelectExoMech(Main.LocalPlayer, true);
        }

        private static void SummonMech(ExoMechType mechType) {
            CWRRef.SummonExo((int)mechType, Main.LocalPlayer);
        }

        private enum ExoMechType
        {
            None = 0,
            Destroyer = 1,
            Prime = 2,
            Twins = 3
        }
    }
}

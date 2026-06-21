using CalamityOverhaul.Content.LegendWeapon.HalibutLegend;
using CalamityOverhaul.Content.Narrative.Data.Modules;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.OldDuke
{
    internal sealed class FirstMetOldDuke : NarrativeScenario, ILocalizedModType
    {
        private const string AcceptLabel = "accept";
        private const string DeclineLabel = "decline";
        private const string FightLabel = "fight";

        private static bool declineWasRepeat;

        public string LocalizationCategory => "ADV";

        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }
        public static LocalizedText L4 { get; private set; }
        public static LocalizedText L5 { get; private set; }
        public static LocalizedText L6 { get; private set; }
        public static LocalizedText B1 { get; private set; }
        public static LocalizedText B2 { get; private set; }
        public static LocalizedText HL1 { get; private set; }
        public static LocalizedText HL2 { get; private set; }
        public static LocalizedText C1 { get; private set; }
        public static LocalizedText C2 { get; private set; }
        public static LocalizedText C3 { get; private set; }
        public static LocalizedText C1Response { get; private set; }
        public static LocalizedText C2Response { get; private set; }
        public static LocalizedText C3Response { get; private set; }

        public override StyleId DefaultStyle => "Sulfsea";

        public override void SetStaticDefaults() {
            L0 = this.GetLocalization(nameof(L0), () => "......");
            L1 = this.GetLocalization(nameof(L1), () => "收起杀气吧，后生。没必要一见面就兵戎相见");
            L2 = this.GetLocalization(nameof(L2), () => "我年纪大了，那套打打杀杀的把戏，我已经厌倦了");
            L3 = this.GetLocalization(nameof(L3), () => "我们可以合作");
            L4 = this.GetLocalization(nameof(L4), () => "我正在搜集一种特殊的东西，海洋残片");
            L5 = this.GetLocalization(nameof(L5), () => "如果你能替我找来更多，我会让你觉得物超所值");
            L6 = this.GetLocalization(nameof(L6), () => "这是一份样本");
            B1 = this.GetLocalization(nameof(B1), () => "你改主意了吗？");
            B2 = this.GetLocalization(nameof(B2), () => "那么再见");
            HL1 = this.GetLocalization(nameof(HL1), () => "老教授......?不过他看起来不认识我了");
            HL2 = this.GetLocalization(nameof(HL2), () => "他的话是可信的，我们是硫磺海大学的同事，他是海洋考古学领域的泰斗");
            C1 = this.GetLocalization(nameof(C1), () => "接受合作");
            C2 = this.GetLocalization(nameof(C2), () => "拒绝合作");
            C3 = this.GetLocalization(nameof(C3), () => "拒绝合作并拔出武器");
            C1Response = this.GetLocalization(nameof(C1Response), () => "很好，希望我们能有一个愉快的合作");
            C2Response = this.GetLocalization(nameof(C2Response), () => "......那我就先离开了，如果你想通了，随时欢迎");
            C3Response = this.GetLocalization(nameof(C3Response), () => "......既然你执意如此，那就让我来称量称量你吧");
        }

        protected override void Build(NarrativeComposer n) {
            OldDukeStoryData data = OldDukeStorySync.Story;

            if (data.OldDukeCooperationDeclined) {
                AddDecision(n, B1.Value, giveSample: false);
                return;
            }

            if (HasHalibut()) {
                n.Say("OldDuke", L0.Value)
                 .Say("OldDuke", L1.Value)
                 .Say("OldDuke", L2.Value)
                 .Say("Helen", "Doubt", HL1.Value)
                 .Say("Helen", "Doubt", HL2.Value)
                 .Say("OldDuke", L3.Value)
                 .Say("OldDuke", L4.Value)
                 .Say("OldDuke", L5.Value);
            }
            else {
                n.Say("OldDuke", L0.Value)
                 .Say("OldDuke", L1.Value)
                 .Say("OldDuke", L2.Value)
                 .Say("OldDuke", L3.Value)
                 .Say("OldDuke", L4.Value)
                 .Say("OldDuke", L5.Value);
            }

            AddDecision(n, L6.Value, giveSample: true);
        }

        private static void AddDecision(NarrativeComposer n, string prompt, bool giveSample) {
            if (giveSample) {
                n.Reward(ModContent.ItemType<Oceanfragments>(), 1, string.Empty);
            }

            n.Choice("OldDuke", prompt, c => c
                .Option("accept", C1.Value, NarrativeTarget.Goto(AcceptLabel), onSelect: () => SetState(OldDukeInteractionState.AcceptedCooperation))
                .Option("decline", C2.Value, NarrativeTarget.Goto(DeclineLabel), onSelect: OnDeclineSelected)
                .Option("fight", C3.Value, NarrativeTarget.Goto(FightLabel), onSelect: () => SetState(OldDukeInteractionState.ChoseToFight)))
             .Label(AcceptLabel)
             .Say("OldDuke", C1Response.Value)
             .End()
             .Label(DeclineLabel)
             .Branch(() => declineWasRepeat, NarrativeTarget.Goto("decline_repeat"), NarrativeTarget.Goto("decline_first"))
             .Label("decline_first")
             .Say("OldDuke", C2Response.Value)
             .End()
             .Label("decline_repeat")
             .Say("OldDuke", B2.Value)
             .End()
             .Label(FightLabel)
             .SayTimed("OldDuke", C3Response.Value, TimedSettings.Of(2f))
             .End();
        }

        private static void OnDeclineSelected() {
            declineWasRepeat = OldDukeStorySync.Story.OldDukeCooperationDeclined;
            SetState(OldDukeInteractionState.DeclinedCooperation);
        }

        private static void SetState(OldDukeInteractionState state) {
            OldDukeStorySync.SetState(Main.LocalPlayer, state);
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

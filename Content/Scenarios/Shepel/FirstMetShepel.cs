using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend;
using CalamityOverhaul.Content.Scenarios.Shepel.CybCourses;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using InnoVault.Narrative.Runtime;
using Terraria;
using Terraria.Audio;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Shepel
{
    internal sealed class FirstMetShepel : NarrativeScenario, ILocalizedModType
    {
        private const string Choice1Label = "choice1";
        private const string Choice2Label = "choice2";
        private const string CybAcceptLabel = "cyb_accept";
        private const string CybDeclineLabel = "cyb_decline";

        public string LocalizationCategory => "ADV.Shepel";

        public static LocalizedText SpeakerName { get; private set; }
        public static LocalizedText Line1 { get; private set; }
        public static LocalizedText Choice1Text { get; private set; }
        public static LocalizedText Choice2Text { get; private set; }
        public static LocalizedText Choice1Silence { get; private set; }
        public static LocalizedText Choice1Response { get; private set; }
        public static LocalizedText CybCourseOfferLine { get; private set; }
        public static LocalizedText CybCourseAcceptText { get; private set; }
        public static LocalizedText CybCourseDeclineText { get; private set; }
        public static LocalizedText CybCourseAcceptResponse { get; private set; }
        public static LocalizedText CybCourseDeclineResponse { get; private set; }

        public override StyleId DefaultStyle => "SHPC";

        public override void SetStaticDefaults() {
            SpeakerName = this.GetLocalization(nameof(SpeakerName), () => "SHPC");
            Line1 = this.GetLocalization(nameof(Line1), () => "主人！很高兴再见到您！");
            Choice1Text = this.GetLocalization(nameof(Choice1Text), () => "你认错人了吧？");
            Choice2Text = this.GetLocalization(nameof(Choice2Text), () => "...好久不见");
            Choice1Silence = this.GetLocalization(nameof(Choice1Silence), () => "......");
            Choice1Response = this.GetLocalization(nameof(Choice1Response), () => "...是的，只要是您，我每次都愿意认错");
            CybCourseOfferLine = this.GetLocalization(nameof(CybCourseOfferLine), () => "超梦程序已成功录入。主人，是否现在接入超梦空间，学习骇客模式及SHPC武器操作规范？");
            CybCourseAcceptText = this.GetLocalization(nameof(CybCourseAcceptText), () => "现在就去");
            CybCourseDeclineText = this.GetLocalization(nameof(CybCourseDeclineText), () => "暂时不去");
            CybCourseAcceptResponse = this.GetLocalization(nameof(CybCourseAcceptResponse), () => "收到，正在建立神经链路……请保持稳定。");
            CybCourseDeclineResponse = this.GetLocalization(nameof(CybCourseDeclineResponse), () => "了解。超梦接入凭证已写入您的存档，主人随时可以自行激活。");
        }

        protected override void Build(NarrativeComposer n) {
            n.Choice("SHPC", Line1.Value, c => c
                    .Option("deny", Choice1Text.Value, NarrativeTarget.Goto(Choice1Label))
                    .Option("reunion", Choice2Text.Value, NarrativeTarget.Goto(Choice2Label), enabled: () => false))
             .Label(Choice1Label)
             .Say("SHPC", Choice1Silence.Value, onEnter: PlayChoice1Glitch)
             .Say("SHPC", Choice1Response.Value, onEnter: ShepelNarrativePortrait.FaceEnter(ShepelFullBodyPortrait.Face.Smirk))
             .Choice("SHPC", CybCourseOfferLine.Value, c => c
                 .Option("accept", CybCourseAcceptText.Value, NarrativeTarget.Goto(CybAcceptLabel))
                 .Option("decline", CybCourseDeclineText.Value, NarrativeTarget.Goto(CybDeclineLabel)))
             .Label(CybAcceptLabel)
             .Say("SHPC", CybCourseAcceptResponse.Value,
                 onEnter: ShepelNarrativePortrait.FaceEnter(ShepelFullBodyPortrait.Face.Serious),
                 onExit: OnAcceptCybCourse)
             .End()
             .Label(CybDeclineLabel)
             .SayReward("SHPC", CybCourseDeclineResponse.Value, ModContent.ItemType<Mewtwo>(), title: string.Empty, onExit: ShepelStorySync.MarkFirstSHPCIntroCompleted)
             .End()
             .Label(Choice2Label)
             .End();
        }

        protected override NarrativePolicy ConfigurePolicy() => new() {
            IsCompleted = _ => ShepelStorySync.ReadShepel(d => d.FirstSHPCObtained, d => d.FirstSHPCObtained),
            CanTrigger = (_, player) => player.HasItem(SHPCOverride.ID),
            OnTriggered = _ => ShepelStorySync.WriteShepel(d => d.FirstSHPCObtained = true, d => d.FirstSHPCObtained = true),
        };

        protected override void OnStarted() => ShepelNarrativePortrait.Show();

        protected override void OnCompleted() => ShepelNarrativePortrait.Hide();

        private static void PlayChoice1Glitch() {
            ShepelNarrativePortrait.TriggerGlitch(1f, 1f);
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(CWRSound.Fault);
            }
        }

        private static void OnAcceptCybCourse() {
            ShepelStorySync.MarkFirstSHPCIntroCompleted();
            CybCourse.ScheduleMewtwoGrant();
            if (Main.myPlayer == Main.LocalPlayer.whoAmI) {
                CybCourse.Enter();
            }
        }
    }
}

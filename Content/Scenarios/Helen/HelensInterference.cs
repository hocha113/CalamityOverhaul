using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.LegendWeapon.HalibutLegend;
using CalamityOverhaul.Content.Narrative;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using InnoVault.Narrative.Runtime;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Helen
{
    internal sealed class HelensInterference : NarrativeScenario, ILocalizedModType
    {
        private const string InquiryLabel = "inquiry";
        private const string AngerLabel = "anger";
        private const string SilenceLabel = "silence";
        private const string ContinueLabel = "continue";
        private const string StopLabel = "stop";

        public static int DelayTimer;

        public string LocalizationCategory => "ADV";

        public static LocalizedText Rolename { get; private set; }
        public static LocalizedText Line0 { get; private set; }
        public static LocalizedText Line1 { get; private set; }
        public static LocalizedText Line2 { get; private set; }
        public static LocalizedText Question1 { get; private set; }
        public static LocalizedText Choice1_1 { get; private set; }
        public static LocalizedText Choice1_2 { get; private set; }
        public static LocalizedText Choice1_3 { get; private set; }
        public static LocalizedText FinalQuestion { get; private set; }
        public static LocalizedText FinalChoice_Continue { get; private set; }
        public static LocalizedText FinalChoice_Stop { get; private set; }

        public static LocalizedText InquiryLine1 { get; private set; }
        public static LocalizedText InquiryLine2 { get; private set; }
        public static LocalizedText InquiryLine3 { get; private set; }
        public static LocalizedText InquiryLine4 { get; private set; }
        public static LocalizedText AngerLine1 { get; private set; }
        public static LocalizedText AngerLine2 { get; private set; }
        public static LocalizedText AngerLine3 { get; private set; }
        public static LocalizedText AngerLine4 { get; private set; }
        public static LocalizedText SilenceLine1 { get; private set; }
        public static LocalizedText SilenceLine2 { get; private set; }
        public static LocalizedText SilenceLine3 { get; private set; }
        public static LocalizedText SilenceLine4 { get; private set; }
        public static LocalizedText ContinueLine1 { get; private set; }
        public static LocalizedText ContinueLine2 { get; private set; }
        public static LocalizedText ContinueLine3 { get; private set; }
        public static LocalizedText ContinueLine4 { get; private set; }
        public static LocalizedText ContinueLine5 { get; private set; }
        public static LocalizedText StopLine1 { get; private set; }
        public static LocalizedText StopLine2 { get; private set; }
        public static LocalizedText StopLine3 { get; private set; }

        public override StyleId DefaultStyle => "Sea";

        public override void SetStaticDefaults() {
            Rolename = this.GetLocalization(nameof(Rolename), () => "比目鱼");
            Line0 = this.GetLocalization(nameof(Line0), () => "……喂，你有空吗？");
            Line1 = this.GetLocalization(nameof(Line1), () => "今天.....嗯....天气不错？");
            Line2 = this.GetLocalization(nameof(Line2), () => "(比目鱼似乎将一些东西藏了起来)");
            Question1 = this.GetLocalization(nameof(Question1), () => "......");
            Choice1_1 = this.GetLocalization(nameof(Choice1_1), () => "你在做什么？");
            Choice1_2 = this.GetLocalization(nameof(Choice1_2), () => "拿出来！");
            Choice1_3 = this.GetLocalization(nameof(Choice1_3), () => "(沉默)");
            FinalQuestion = this.GetLocalization(nameof(FinalQuestion), () => "不要再往前走了.....好吗。那个女巫，她的委托，她想要的东西......我不想看到。剩下的路我们完全可以自己走");
            FinalChoice_Continue = this.GetLocalization(nameof(FinalChoice_Continue), () => "继续委托");
            FinalChoice_Stop = this.GetLocalization(nameof(FinalChoice_Stop), () => "中止委托");

            InquiryLine1 = this.GetLocalization("Branch_Inquiry.Line1", () => "我最近愈发不安");
            InquiryLine2 = this.GetLocalization("Branch_Inquiry.Line2", () => "那把刀，我碰到它的时候，像是听到了......有人在窃笑");
            InquiryLine3 = this.GetLocalization("Branch_Inquiry.Line3", () => "它们都像是......某种'媒介物品'");
            InquiryLine4 = this.GetLocalization("Branch_Inquiry.Line4", () => "我不能让你带着它走下去，至少......");
            AngerLine1 = this.GetLocalization("Branch_Anger.Line1", () => "......你真的...信她到了这种地步？");
            AngerLine2 = this.GetLocalization("Branch_Anger.Line2", () => "难道你没发现吗？那个女巫，她每次给你的东西都不太正常");
            AngerLine3 = this.GetLocalization("Branch_Anger.Line3", () => "这让我回想起深渊中的某个......我碰到它......阴冷、恐怖、令人发悸...啊....我不想回忆");
            AngerLine4 = this.GetLocalization("Branch_Anger.Line4", () => "你本该有所察觉的.......");
            SilenceLine1 = this.GetLocalization("Branch_Silence.Line1", () => "......你沉默的时候最可怕");
            SilenceLine2 = this.GetLocalization("Branch_Silence.Line2", () => "好吧，我把刻心者藏起来了......");
            SilenceLine3 = this.GetLocalization("Branch_Silence.Line3", () => "那个女巫，来路不明，危险至极。如果那是陷阱，你也要乖乖跳进去吗？");
            SilenceLine4 = this.GetLocalization("Branch_Silence.Line4", () => "停下来，权当是......为了我");
            ContinueLine1 = this.GetLocalization("FinalBranch_Continue.Line1", () => "......好吧。我知道你会这么选");
            ContinueLine2 = this.GetLocalization("FinalBranch_Continue.Line2", () => "......");
            ContinueLine3 = this.GetLocalization("FinalBranch_Continue.Line3", () => "东西我会放回去，但我希望你用它的时候......谨慎一点");
            ContinueLine4 = this.GetLocalization("FinalBranch_Continue.Line4", () => "还有那个女巫，也......");
            ContinueLine5 = this.GetLocalization("FinalBranch_Continue.Line5", () => "我会陪你走下去......一直");
            StopLine1 = this.GetLocalization("FinalBranch_Stop.Line1", () => "......你真的愿意停下来吗");
            StopLine2 = this.GetLocalization("FinalBranch_Stop.Line2", () => "......");
            StopLine3 = this.GetLocalization("FinalBranch_Stop.Line3", () => "我们走吧，让那个女巫下地狱去吧");
        }

        protected override void Build(NarrativeComposer n) {
            n.Say("Helen", "Serious", Line0.Value)
             .Say("Helen", "Serious", Line1.Value)
             .Say("System", Line2.Value, onExit: RemoveHeartcarverFromPlayer)
             .Choice("Helen", "Serious", Question1.Value, c => c
                 .Option("inquiry", Choice1_1.Value, NarrativeTarget.Goto(InquiryLabel))
                 .Option("anger", Choice1_2.Value, NarrativeTarget.Goto(AngerLabel))
                 .Option("silence", Choice1_3.Value, NarrativeTarget.Goto(SilenceLabel)))
             .Label(InquiryLabel)
             .Say("Helen", "Serious", InquiryLine1.Value)
             .Say("Helen", "Serious", InquiryLine2.Value)
             .Say("Helen", "Serious", InquiryLine3.Value)
             .Say("Helen", "Serious", InquiryLine4.Value)
             .Choice("Helen", "Serious", FinalQuestion.Value, c => c
                 .Option("continue", FinalChoice_Continue.Value, NarrativeTarget.Goto(ContinueLabel))
                 .Option("stop", FinalChoice_Stop.Value, NarrativeTarget.Goto(StopLabel)))
             .Label(AngerLabel)
             .Say("Helen", "Serious", AngerLine1.Value)
             .Say("Helen", "Serious", AngerLine2.Value)
             .Say("Helen", "Serious", AngerLine3.Value)
             .Say("Helen", "Serious", AngerLine4.Value)
             .Choice("Helen", "Serious", FinalQuestion.Value, c => c
                 .Option("continue", FinalChoice_Continue.Value, NarrativeTarget.Goto(ContinueLabel))
                 .Option("stop", FinalChoice_Stop.Value, NarrativeTarget.Goto(StopLabel)))
             .Label(SilenceLabel)
             .Say("Helen", "Serious", SilenceLine1.Value)
             .Say("Helen", "Serious", SilenceLine2.Value)
             .Say("Helen", "Serious", SilenceLine3.Value)
             .Say("Helen", "Serious", SilenceLine4.Value)
             .Choice("Helen", "Serious", FinalQuestion.Value, c => c
                 .Option("continue", FinalChoice_Continue.Value, NarrativeTarget.Goto(ContinueLabel))
                 .Option("stop", FinalChoice_Stop.Value, NarrativeTarget.Goto(StopLabel)))
             .Label(ContinueLabel)
             .Say("Helen", "Serious", ContinueLine1.Value)
             .Say("Helen", "Serious", ContinueLine2.Value)
             .Say("Helen", "Serious", ContinueLine3.Value)
             .Say("Helen", "Serious", ContinueLine4.Value)
             .Say("Helen", "Serious", ContinueLine5.Value, onExit: ReturnHeartcarverToPlayer)
             .End()
             .Label(StopLabel)
             .Say("Helen", StopLine1.Value)
             .Say("Helen", StopLine2.Value)
             .Say("Helen", StopLine3.Value, onExit: PlayStopSound)
             .End();
        }

        public static void ResetWorldState() => DelayTimer = 0;

        public static void Tick() {
            if (!HalibutStorySync.ReadSupCal(d => d.SupCalDoGQuestAccepted, d => d.SupCalDoGQuestAccepted)) {
                return;
            }

            if (HalibutStorySync.ReadSupCal(d => d.HelenInterferenceTriggered, d => d.HelenInterferenceTriggered)) {
                return;
            }

            if (HalibutStorySync.ReadSupCal(
                    d => d.SupCalDoGQuestReward || d.SupCalDoGQuestDeclined,
                    d => d.SupCalDoGQuestReward || d.SupCalDoGQuestDeclined)) {
                return;
            }

            if (CWRWorld.HasBoss || NarrativeTriggerGate.IsBusy) {
                return;
            }

            Player player = Main.LocalPlayer;
            if (!player.TryGetOverride(out HalibutPlayer halibutPlayer) || !halibutPlayer.HasHalubut) {
                return;
            }

            if (--DelayTimer > 0) {
                return;
            }

            if (NarrativeRouter.Begin<HelensInterference>()) {
                HalibutStorySync.WriteSupCal(
                    d => d.HelenInterferenceTriggered = true,
                    d => d.HelenInterferenceTriggered = true);
            }
        }

        private static void RemoveHeartcarverFromPlayer() {
            Player player = Main.LocalPlayer;
            int heartcarverType = ModContent.ItemType<Heartcarver>();
            for (int i = 0; i < player.inventory.Length; i++) {
                if (player.inventory[i].type == heartcarverType) {
                    player.inventory[i].TurnToAir();
                }
            }
        }

        private static void ReturnHeartcarverToPlayer() {
            Player player = Main.LocalPlayer;
            player.QuickSpawnItem(player.GetSource_Misc("HelensInterference"), ModContent.ItemType<Heartcarver>(), 1);
            SoundEngine.PlaySound(SoundID.Grab with { Volume = 0.7f, Pitch = 0.2f }, player.Center);
            HalibutStorySync.WriteSupCal(
                d => d.HelenInterferenceContinue = true,
                d => d.HelenInterferenceContinue = true);
        }

        private static void PlayStopSound() {
            HalibutStorySync.WriteSupCal(
                d => {
                    d.SupCalDoGQuestDeclined = true;
                    d.HelenInterferenceStop = true;
                },
                d => {
                    d.SupCalDoGQuestDeclined = true;
                    d.HelenInterferenceStop = true;
                });
            SoundEngine.PlaySound(SoundID.NPCDeath1 with { Volume = 0.6f, Pitch = -0.4f }, Main.LocalPlayer.Center);
        }
    }
}

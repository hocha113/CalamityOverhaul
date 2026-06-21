using CalamityOverhaul.Content.EntrustManager;
using CalamityOverhaul.Content.Items.Tools;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Helen.Quest.FishoilQuest
{
    internal sealed class FishoilSubmitScenario : NarrativeScenario, ILocalizedModType
    {
        private const string GiveOkLabel = "give_ok";
        private const string NotEnoughLabel = "not_enough";
        private const string RefuseLabel = "refuse";

        private static bool giveSucceeded;

        public string LocalizationCategory => "ADV";
        public static LocalizedText SubmitLine1 { get; private set; }
        public static LocalizedText SubmitLine2 { get; private set; }
        public static LocalizedText QuestionLine { get; private set; }
        public static LocalizedText ChoiceGive { get; private set; }
        public static LocalizedText ChoiceRefuse { get; private set; }
        public static LocalizedText GiveResponse { get; private set; }
        public static LocalizedText RefuseResponse { get; private set; }
        public static LocalizedText NotEnoughResponse { get; private set; }
        public static LocalizedText AlreadyCompletedResponse { get; private set; }

        public override StyleId DefaultStyle => "Sea";

        public override void SetStaticDefaults() {
            SubmitLine1 = this.GetLocalization(nameof(SubmitLine1), () => "嗯，你收集到了足够的鱼");
            SubmitLine2 = this.GetLocalization(nameof(SubmitLine2), () => "正好够我提炼一瓶鱼油的量");
            QuestionLine = this.GetLocalization(nameof(QuestionLine), () => "你要把它们交给我吗？");
            ChoiceGive = this.GetLocalization(nameof(ChoiceGive), () => "好，拿去吧");
            ChoiceRefuse = this.GetLocalization(nameof(ChoiceRefuse), () => "我再想想");
            GiveResponse = this.GetLocalization(nameof(GiveResponse), () => "很好，稍等...给你，一瓶新鲜的鱼油");
            RefuseResponse = this.GetLocalization(nameof(RefuseResponse), () => "好吧，什么时候想好了再来找我");
            NotEnoughResponse = this.GetLocalization(nameof(NotEnoughResponse), () => "嗯？鱼好像不太够，再去捕一些回来吧");
            AlreadyCompletedResponse = this.GetLocalization(nameof(AlreadyCompletedResponse), () => "鱼油已经给你了，再要的话还得等下次");

            FishoilQuestEntry.InitLocalization(this);
        }

        protected override void Build(NarrativeComposer n) {
            if (FishoilQuestEntry.IsPersistentlyCompleted()) {
                n.Say("Helen", AlreadyCompletedResponse.Value).End();
                return;
            }

            if (FishoilQuestEntry.CountAvailableFish(Main.LocalPlayer) < FishoilQuestEntry.FishRequired) {
                n.Say("Helen", NotEnoughResponse.Value).End();
                return;
            }

            n.Say("Helen", SubmitLine1.Value)
             .Say("Helen", "Enjoy", SubmitLine2.Value)
             .Choice("Helen", QuestionLine.Value, c => c
                 .Option("give", ChoiceGive.Value, NarrativeTarget.Goto(GiveOkLabel))
                 .Option("refuse", ChoiceRefuse.Value, NarrativeTarget.Goto(RefuseLabel), onSelect: SuspendQuest))
             .Label(GiveOkLabel)
             .Command(() => giveSucceeded = TryConsumeFish(Main.LocalPlayer))
             .Branch(() => giveSucceeded, NarrativeTarget.Goto("reward"), NarrativeTarget.Goto(NotEnoughLabel))
             .Label("reward")
             .Reward(ModContent.ItemType<Fishoil>(), 5, string.Empty)
             .Say("Helen", "Enjoy", GiveResponse.Value)
             .End()
             .Label(NotEnoughLabel)
             .Say("Helen", NotEnoughResponse.Value)
             .End()
             .Label(RefuseLabel)
             .Say("Helen", RefuseResponse.Value)
             .End();
        }

        private static bool TryConsumeFish(Player player) {
            if (player == null || FishoilQuestEntry.IsPersistentlyCompleted()) {
                return true;
            }

            int needed = FishoilQuestEntry.FishRequired;
            if (FishoilQuestEntry.CountAvailableFish(player) < needed) {
                return false;
            }

            int consumed = FishoilQuestEntry.ConsumeAvailableFish(player, needed);
            if (consumed < needed) {
                return false;
            }

            HalibutStorySync.WriteHalibut(
                d => {
                    d.FishoilQuestCompleted = true;
                    d.FishoilQuestSuspended = false;
                },
                d => {
                    d.FishoilQuestCompleted = true;
                    d.FishoilQuestSuspended = false;
                });
            QuestManagerUI.Instance?.SetEntryStatus(FishoilQuestEntry.QuestKey, QuestEntryStatus.Completed, 1f);
            return true;
        }

        private static void SuspendQuest() {
            if (FishoilQuestEntry.IsPersistentlyCompleted()) {
                return;
            }

            HalibutStorySync.WriteHalibut(
                d => d.FishoilQuestSuspended = true,
                d => d.FishoilQuestSuspended = true);
        }
    }
}

using CalamityOverhaul.Content.EntrustManager;
using CalamityOverhaul.Content.Narrative;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using InnoVault.Narrative.Runtime;
using System.Linq;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Helen.Quest.FishoilQuest
{
    internal sealed class FishoilQuestScenario : NarrativeScenario, ILocalizedModType
    {
        public string LocalizationCategory => "ADV.Helen";

        public static bool Spwand;
        private static bool scenarioStarted;
        private static int spawnDelayTimer;

        private const int FishNeedThreshold = 10;

        internal static readonly int[] CandidateFishTypes = [
            ItemID.Bass,
            ItemID.Trout,
            ItemID.Salmon,
            ItemID.Tuna,
            ItemID.RedSnapper,
            ItemID.NeonTetra,
            ItemID.Damselfish,
            ItemID.ArmoredCavefish,
            ItemID.Hemopiranha,
            ItemID.Ebonkoi,
            ItemID.SpecularFish,
            ItemID.Prismite
        ];
        public static LocalizedText Line0 { get; private set; }
        public static LocalizedText Line1 { get; private set; }
        public static LocalizedText Line2 { get; private set; }
        public static LocalizedText Line3 { get; private set; }
        public static LocalizedText Line4 { get; private set; }
        public static LocalizedText ChoiceAccept { get; private set; }
        public static LocalizedText ChoiceDecline { get; private set; }

        public override StyleId DefaultStyle => "Sea";

        public override void SetStaticDefaults() {
            Line0 = this.GetLocalization(nameof(Line0), () => "你最近好像捕到了不少普通的鱼");
            Line1 = this.GetLocalization(nameof(Line1), () => "给我一些做实验，我可以提炼出一瓶新鲜的鱼油");
            Line2 = this.GetLocalization(nameof(Line2), () => "过程不难但很枯燥");
            Line3 = this.GetLocalization(nameof(Line3), () => "鱼油很有潜力,比你想的更有用");
            Line4 = this.GetLocalization(nameof(Line4), () => "愿意吗?");
            ChoiceAccept = this.GetLocalization(nameof(ChoiceAccept), () => "可以");
            ChoiceDecline = this.GetLocalization(nameof(ChoiceDecline), () => "没兴趣");
        }

        protected override void Build(NarrativeComposer n) {
            n.Say("Helen", Line0.Value)
             .Say("Helen", Line1.Value)
             .Say("Helen", Line2.Value)
             .Say("Helen", "Enjoy", Line3.Value)
             .Choice("Helen", Line4.Value, c => c
                 .Option("accept", ChoiceAccept.Value, onSelect: AcceptQuest)
                 .Option("decline", ChoiceDecline.Value, onSelect: DeclineQuest));
        }

        public static void ResetWorldState() {
            Spwand = false;
            scenarioStarted = false;
            spawnDelayTimer = 0;
        }

        public static void Tick() {
            SyncQuestEntry();

            Player player = Main.LocalPlayer;
            if (!NPC.downedQueenBee) {
                Spwand = false;
                return;
            }

            if (!HalibutStorySync.ReadHalibut(d => d.FirstMet, d => d.FirstMet)) {
                return;
            }

            if (HalibutStorySync.ReadHalibut(d => d.FishoilQuestAccepted || d.FishoilQuestDeclined, d => d.FishoilQuestAccepted || d.FishoilQuestDeclined)) {
                return;
            }

            if (scenarioStarted || NarrativeRunner.IsScenarioActiveOrPending(GetKey<FishoilQuestScenario>())) {
                return;
            }

            int totalFishCount = 0;
            for (int i = 0; i < player.inventory.Length; i++) {
                Item item = player.inventory[i];
                if (item != null && item.stack > 0 && CandidateFishTypes.Contains(item.type)) {
                    totalFishCount += item.stack;
                    if (totalFishCount >= FishNeedThreshold) {
                        break;
                    }
                }
            }

            if (totalFishCount < FishNeedThreshold) {
                return;
            }

            if (!Spwand) {
                Spwand = true;
                spawnDelayTimer = Main.rand.Next(60, 160);
            }

            if (spawnDelayTimer > 0) {
                spawnDelayTimer--;
                return;
            }

            if (VaultUtils.IsInvasion() || CWRWorld.HasBoss || NarrativeTriggerGate.IsBusy) {
                return;
            }

            if (NarrativeRouter.Begin<FishoilQuestScenario>()) {
                scenarioStarted = true;
                Spwand = false;
            }
        }

        internal static void RegisterQuestEntry(bool completed = false, bool notify = true) {
            QuestManagerUI manager = QuestManagerUI.Instance;
            if (manager == null) {
                return;
            }

            if (manager.GetEntry(FishoilQuestEntry.QuestKey) is EntrustEntryData existing) {
                if (existing is FishoilQuestEntry) {
                    return;
                }

                manager.UnregisterQuest(FishoilQuestEntry.QuestKey);
            }

            FishoilQuestEntry entry = FishoilQuestEntry.Create();
            if (completed) {
                entry.Status = QuestEntryStatus.Completed;
                entry.Progress = 1f;
            }
            else if (!notify) {
                entry.Status = QuestEntryStatus.Tracked;
                entry.IsNew = false;
            }

            manager.RegisterQuest(entry);
        }

        private static void SyncQuestEntry() {
            QuestManagerUI manager = QuestManagerUI.Instance;
            if (manager == null) {
                return;
            }

            if (!HalibutStorySync.ReadHalibut(d => d.FishoilQuestAccepted, d => d.FishoilQuestAccepted)) {
                manager.UnregisterQuest(FishoilQuestEntry.QuestKey);
                return;
            }

            bool completed = HalibutStorySync.ReadHalibut(d => d.FishoilQuestCompleted, d => d.FishoilQuestCompleted);
            RegisterQuestEntry(completed, notify: false);
            EntrustEntryData entry = manager.GetEntry(FishoilQuestEntry.QuestKey);
            if (entry == null) {
                return;
            }

            if (completed && entry.Status != QuestEntryStatus.Completed) {
                entry.Status = QuestEntryStatus.Completed;
                entry.Progress = 1f;
                manager.MarkFilterDirty();
            }
        }

        private static void AcceptQuest() {
            HalibutStorySync.WriteHalibut(d => d.FishoilQuestAccepted = true, d => d.FishoilQuestAccepted = true);
            RegisterQuestEntry();
            scenarioStarted = false;
        }

        private static void DeclineQuest() {
            HalibutStorySync.WriteHalibut(d => d.FishoilQuestDeclined = true, d => d.FishoilQuestDeclined = true);
            scenarioStarted = false;
        }

        protected override void OnCompleted() {
            scenarioStarted = false;
        }
    }
}

using CalamityOverhaul.Content.EntrustManager;
using CalamityOverhaul.Content.Scenarios.Helen;
using CalamityOverhaul.Content.Scenarios.SupCal.Quest.DoGQuest;
using CalamityOverhaul.Content.Scenarios.SupCal.Quest.PallbearerQuest;
using CalamityOverhaul.Content.Scenarios.SupCal.Quest.YharonQuest;
using System;

using Terraria;

using Terraria.Localization;

using Terraria.ModLoader;



namespace CalamityOverhaul.Content.Scenarios.SupCal.Quest

{

    /// <summary>硫火女巫委托线，三段注册到 <see cref="QuestManagerUI"/></summary>

    internal sealed class SupCalQuestLine : ModSystem, ILocalizedModType

    {

        private const string PallbearerKey = "SupCal_Pallbearer";

        private const string DoGKey = "SupCal_DoG";

        private const string YharonKey = "SupCal_Yharon";



        public string LocalizationCategory => "ADV";



        public static LocalizedText QuestCategory { get; private set; }

        public static LocalizedText PallbearerTitle { get; private set; }

        public static LocalizedText PallbearerSummary { get; private set; }

        public static LocalizedText DoGTitle { get; private set; }

        public static LocalizedText DoGSummary { get; private set; }

        public static LocalizedText YharonTitle { get; private set; }

        public static LocalizedText YharonSummary { get; private set; }

        public static LocalizedText TrackerSummonHint { get; private set; }

        public static LocalizedText TrackerContribution { get; private set; }

        public static LocalizedText TrackerRequired { get; private set; }



        public override void SetStaticDefaults() {

            QuestCategory = this.GetLocalization(nameof(QuestCategory), () => "硫火女巫");



            PallbearerTitle = this.GetLocalization(nameof(PallbearerTitle), () => "委托：猎杀亵渎天神");

            PallbearerSummary = this.GetLocalization(nameof(PallbearerSummary), () => "使用扶柩者击杀亵渎天神，贡献度需达到80%");



            DoGTitle = this.GetLocalization(nameof(DoGTitle), () => "委托：猎杀神明吞噬者");

            DoGSummary = this.GetLocalization(nameof(DoGSummary), () => "使用刻心者击杀神明吞噬者，贡献度需达到80%");



            YharonTitle = this.GetLocalization(nameof(YharonTitle), () => "委托：猎杀焚世龙");

            YharonSummary = this.GetLocalization(nameof(YharonSummary), () => "使用鬼面刀击杀焚世之龙，贡献度需达到75%");



            TrackerSummonHint = this.GetLocalization(nameof(TrackerSummonHint), () => "目标不在场，请召唤 {0}");

            TrackerContribution = this.GetLocalization(nameof(TrackerContribution), () => "武器贡献: {0:0%}");

            TrackerRequired = this.GetLocalization(nameof(TrackerRequired), () => "需求: {0:0%}");

        }



        public override void PostUpdateEverything() {

            if (Main.dedServ || Main.gameMenu) {

                return;

            }



            QuestManagerUI manager = QuestManagerUI.Instance;

            if (manager == null) {

                return;

            }



            SyncQuest(manager, PallbearerKey,

                PallbearerTitle, PallbearerSummary,

                CWRID.NPC_Providence,

                PallbearerQuestTracker.REQUIRED_CONTRIBUTION,

                prerequisite: HalibutStorySync.ReadSupCal(d => d.SupCalMoonLordReward, d => d.SupCalMoonLordReward),

                accepted: HalibutStorySync.ReadSupCal(d => d.SupCalQuestAccepted, d => d.SupCalQuestAccepted),

                declined: HalibutStorySync.ReadSupCal(d => d.SupCalQuestDeclined, d => d.SupCalQuestDeclined),

                completed: HalibutStorySync.ReadSupCal(d => d.SupCalQuestReward, d => d.SupCalQuestReward),

                priority: 30,

                onUnsuspended: () => HalibutStorySync.WriteSupCal(

                    d => { d.SupCalQuestDeclined = false; d.SupCalQuestAccepted = true; },

                    d => { d.SupCalQuestDeclined = false; d.SupCalQuestAccepted = true; }));



            SyncQuest(manager, DoGKey,

                DoGTitle, DoGSummary,

                CWRID.NPC_DevourerofGodsHead,

                DoGQuestTracker.REQUIRED_CONTRIBUTION,

                prerequisite: HalibutStorySync.ReadSupCal(d => d.SupCalQuestReward, d => d.SupCalQuestReward),

                accepted: HalibutStorySync.ReadSupCal(d => d.SupCalDoGQuestAccepted, d => d.SupCalDoGQuestAccepted),

                declined: HalibutStorySync.ReadSupCal(d => d.SupCalDoGQuestDeclined, d => d.SupCalDoGQuestDeclined),

                completed: HalibutStorySync.ReadSupCal(d => d.SupCalDoGQuestReward, d => d.SupCalDoGQuestReward),

                priority: 20,

                onUnsuspended: () => HalibutStorySync.WriteSupCal(

                    d => { d.SupCalDoGQuestDeclined = false; d.SupCalDoGQuestAccepted = true; },

                    d => { d.SupCalDoGQuestDeclined = false; d.SupCalDoGQuestAccepted = true; }));



            SyncQuest(manager, YharonKey,

                YharonTitle, YharonSummary,

                CWRID.NPC_Yharon,

                YharonQuestTracker.REQUIRED_CONTRIBUTION,

                prerequisite: HalibutStorySync.ReadSupCal(d => d.SupCalDoGQuestReward, d => d.SupCalDoGQuestReward),

                accepted: HalibutStorySync.ReadSupCal(d => d.SupCalYharonQuestAccepted, d => d.SupCalYharonQuestAccepted),

                declined: HalibutStorySync.ReadSupCal(d => d.SupCalYharonQuestDeclined, d => d.SupCalYharonQuestDeclined),

                completed: HalibutStorySync.ReadSupCal(d => d.SupCalYharonQuestReward, d => d.SupCalYharonQuestReward),

                priority: 10,

                onUnsuspended: () => HalibutStorySync.WriteSupCal(

                    d => { d.SupCalYharonQuestDeclined = false; d.SupCalYharonQuestAccepted = true; },

                    d => { d.SupCalYharonQuestDeclined = false; d.SupCalYharonQuestAccepted = true; }));

        }



        private static void SyncQuest(

            QuestManagerUI manager, string key,

            LocalizedText title, LocalizedText summary,

            int targetNpcType, float requiredContribution,

            bool prerequisite, bool accepted, bool declined, bool completed,

            int priority,

            Action onUnsuspended = null) {

            if (!prerequisite) {

                manager.UnregisterQuest(key);

                return;

            }



            if (completed) {

                EnsureQuestEntry(manager, key, title, summary, targetNpcType, requiredContribution,

                    priority, QuestEntryStatus.Completed, onUnsuspended);

                manager.SetEntryStatus(key, QuestEntryStatus.Completed, 1f);

                return;

            }



            if (declined) {

                EnsureQuestEntry(manager, key, title, summary, targetNpcType, requiredContribution,

                    priority, QuestEntryStatus.Suspended, onUnsuspended);

                return;

            }



            if (!accepted) {

                manager.UnregisterQuest(key);

                return;

            }



            EntrustEntryData activeEntry = EnsureQuestEntry(manager, key, title, summary, targetNpcType,

                requiredContribution, priority, QuestEntryStatus.Active, onUnsuspended);



            if (activeEntry.Status == QuestEntryStatus.Completed) {

                manager.SetEntryStatus(key, QuestEntryStatus.Active, 0f);

            }

        }



        private static EntrustEntryData EnsureQuestEntry(

            QuestManagerUI manager, string key,

            LocalizedText title, LocalizedText summary,

            int targetNpcType, float requiredContribution,

            int priority, QuestEntryStatus status,

            Action onUnsuspended = null) {

            EntrustEntryData entry = manager.GetEntry(key);

            if (entry != null) {

                return entry;

            }



            entry = new SupCalHuntQuestEntry(key, title, summary, QuestCategory) {

                Priority = priority,

                Status = status,

                Progress = status == QuestEntryStatus.Completed ? 1f : 0f,

                Provider = EntrustProviders.SupCal,

                TrackerStyle = new BrimstoneTrackerWidgetStyle(),

                TargetNpcType = targetNpcType,

                RequiredContribution = requiredContribution,

                SummonHintFormat = TrackerSummonHint,

                ContributionFormat = TrackerContribution,

                RequiredFormat = TrackerRequired,

                OnUnsuspended = onUnsuspended

            };

            manager.RegisterQuest(entry);

            return entry;

        }

    }

}


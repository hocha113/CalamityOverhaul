using CalamityOverhaul.Content.ADV.EntrustManager;
using CalamityOverhaul.Content.ADV.Scenarios.SupCal.Quest.DoGQuest;
using CalamityOverhaul.Content.ADV.Scenarios.SupCal.Quest.PallbearerQuest;
using CalamityOverhaul.Content.ADV.Scenarios.SupCal.Quest.YharonQuest;
using System;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.SupCal.Quest
{
    /// <summary>
    /// 硫火女巫委托线，三段委托注册到 <see cref="QuestManagerUI"/>
    /// </summary>
    internal class SupCalQuestLine : ModSystem, ILocalizedModType
    {
        public string LocalizationCategory => "ADV";

        #region 常量 Key

        private const string PALLBEARER_KEY = "SupCal_Pallbearer";
        private const string DOG_KEY = "SupCal_DoG";
        private const string YHARON_KEY = "SupCal_Yharon";

        #endregion

        #region 本地化文本

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

        #endregion

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
            if (Main.dedServ || Main.gameMenu) return;

            var manager = QuestManagerUI.Instance;
            if (manager == null) return;

            if (!Main.LocalPlayer.TryGetADVSave(out var save)) return;
            var sc = save.Get<SupCalADVData>();

            SyncQuest(manager, save, PALLBEARER_KEY,
                PallbearerTitle, PallbearerSummary,
                CWRID.NPC_Providence,
                PallbearerQuestTracker.REQUIRED_CONTRIBUTION,
                prerequisite: sc.SupCalMoonLordReward,
                accepted: sc.SupCalQuestAccepted,
                declined: sc.SupCalQuestDeclined,
                completed: sc.SupCalQuestReward,
                priority: 30,
                onUnsuspended: () => { sc.SupCalQuestDeclined = false; sc.SupCalQuestAccepted = true; });

            SyncQuest(manager, save, DOG_KEY,
                DoGTitle, DoGSummary,
                CWRID.NPC_DevourerofGodsHead,
                DoGQuestTracker.REQUIRED_CONTRIBUTION,
                prerequisite: sc.SupCalQuestReward,
                accepted: sc.SupCalDoGQuestAccepted,
                declined: sc.SupCalDoGQuestDeclined,
                completed: sc.SupCalDoGQuestReward,
                priority: 20,
                onUnsuspended: () => { sc.SupCalDoGQuestDeclined = false; sc.SupCalDoGQuestAccepted = true; });

            SyncQuest(manager, save, YHARON_KEY,
                YharonTitle, YharonSummary,
                CWRID.NPC_Yharon,
                YharonQuestTracker.REQUIRED_CONTRIBUTION,
                prerequisite: sc.SupCalDoGQuestReward,
                accepted: sc.SupCalYharonQuestAccepted,
                declined: sc.SupCalYharonQuestDeclined,
                completed: sc.SupCalYharonQuestReward,
                priority: 10,
                onUnsuspended: () => { sc.SupCalYharonQuestDeclined = false; sc.SupCalYharonQuestAccepted = true; });
        }

        /// <summary>
        /// 同步单条猎杀委托的注册与状态<br/>
        /// 前置未满足 → 从管理器移除<br/>
        /// 已拒绝 → 注册为 <see cref="SupCalHuntQuestEntry"/> 并设为 Suspended，允许玩家在管理器中取消挂起<br/>
        /// 已接受 → 注册为 <see cref="SupCalHuntQuestEntry"/> 并设为 Active<br/>
        /// 已完成 → 标记 Completed
        /// </summary>
        private static void SyncQuest(
            QuestManagerUI manager, ADVSave save, string key,
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

            //已拒绝 → 以挂起状态注册到管理器，允许玩家手动取消挂起重新接受
            if (declined) {
                EnsureQuestEntry(manager, key, title, summary, targetNpcType, requiredContribution,
                    priority, QuestEntryStatus.Suspended, onUnsuspended);
                return;
            }

            if (!accepted) {
                manager.UnregisterQuest(key);
                return;
            }

            var activeEntry = EnsureQuestEntry(manager, key, title, summary, targetNpcType,
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
            var entry = manager.GetEntry(key);
            if (entry != null) return entry;

            entry = new SupCalHuntQuestEntry(key, title, summary, QuestCategory) {
                Priority = priority,
                Status = status,
                Progress = status == QuestEntryStatus.Completed ? 1f : 0f,
                EntryStyle = new BrimstoneEntryStyle(),
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

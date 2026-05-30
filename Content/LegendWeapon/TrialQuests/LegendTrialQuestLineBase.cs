using CalamityOverhaul.Content.ADV.EntrustManager;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.TrialQuests
{
    internal abstract class LegendTrialQuestLineBase : ModSystem
    {
        protected abstract string KeyPrefix { get; }
        protected abstract int LegacyTrialCount { get; }
        protected abstract LocalizedText QuestCategoryText { get; }
        protected abstract LocalizedText TrackerWaitingText { get; }
        protected abstract LocalizedText TrackerFightingText { get; }
        protected abstract LocalizedText TrackerBriefText { get; }
        protected abstract IReadOnlyList<LegendTrialDefinition> Trials { get; }
        protected abstract bool CanCreateEntries(Player player);
        protected abstract IEntrustEntryStyle CreateEntryStyle();
        protected abstract IEntrustTrackerWidgetStyle CreateTrackerStyle();
        protected abstract Func<bool> CreateTrackerVisibilityCheck();

        public override void PostUpdateEverything() {
            if (Main.dedServ || Main.gameMenu) {
                return;
            }

            var manager = QuestManagerUI.Instance;
            if (manager == null || Trials == null) {
                return;
            }

            bool allowCreate = CanCreateEntries(Main.LocalPlayer);
            IReadOnlyList<LegendTrialDefinition> availableTrials = LegendTrialRouteResolver.GetAvailableTrials(Trials);
            int currentLevel = LegendTrialRouteResolver.GetSequentialLevel(Trials);

            for (int i = 0; i < LegacyTrialCount; i++) {
                manager.UnregisterQuest(KeyPrefix + i);
            }

            foreach (LegendTrialDefinition trial in Trials) {
                int routeIndex = IndexOfTrial(availableTrials, trial);
                SyncTrial(manager, trial, routeIndex, currentLevel, allowCreate, availableTrials.Count);
            }
        }

        private void SyncTrial(QuestManagerUI manager, LegendTrialDefinition trial, int routeIndex, int currentLevel, bool allowCreate, int routeCount) {
            string key = GetEntryKey(trial);
            if (trial == null || routeIndex < 0 || routeIndex > currentLevel) {
                manager.UnregisterQuest(key);
                return;
            }

            bool isDone = routeIndex < currentLevel || trial.IsCompleted;
            if (isDone) {
                var entry = EnsureTrialEntry(manager, trial, routeIndex, routeCount, completed: true, allowCreate: allowCreate);
                if (entry != null && entry.Status != QuestEntryStatus.Completed) {
                    manager.SetEntryStatus(key, QuestEntryStatus.Completed, 1f);
                }
            }
            else {
                var entry = EnsureTrialEntry(manager, trial, routeIndex, routeCount, allowCreate: allowCreate);
                if (entry != null && entry.Status == QuestEntryStatus.Completed) {
                    manager.SetEntryStatus(key, QuestEntryStatus.Active, 0f);
                }
            }
        }

        private LegendTrialQuestEntry EnsureTrialEntry(QuestManagerUI manager, LegendTrialDefinition trial, int routeIndex, int routeCount, bool completed = false, bool allowCreate = true) {
            string key = GetEntryKey(trial);
            var entry = manager.GetEntry(key) as LegendTrialQuestEntry;
            if (entry != null) {
                return entry;
            }
            if (!allowCreate) {
                return null;
            }

            entry = CreateTrialEntry(trial, routeIndex, routeCount);
            if (completed) {
                entry.Status = QuestEntryStatus.Completed;
                entry.Progress = 1f;
            }
            manager.RegisterQuest(entry);
            return entry;
        }

        protected virtual LegendTrialQuestEntry CreateTrialEntry(LegendTrialDefinition trial, int routeIndex, int routeCount) {
            return new LegendTrialQuestEntry(GetEntryKey(trial), trial.Title, trial.Summary, QuestCategoryText) {
                Trial = trial,
                Priority = routeCount - routeIndex,
                EntryStyle = CreateEntryStyle(),
                TrackerStyle = CreateTrackerStyle(),
                WaitingHint = TrackerWaitingText,
                FightingFormat = TrackerFightingText,
                BriefFormat = TrackerBriefText,
                TrackerVisibilityCheck = CreateTrackerVisibilityCheck(),
            };
        }

        private string GetEntryKey(LegendTrialDefinition trial) => KeyPrefix + trial.Key;

        private static int IndexOfTrial(IReadOnlyList<LegendTrialDefinition> trials, LegendTrialDefinition target) {
            for (int i = 0; i < trials.Count; i++) {
                if (ReferenceEquals(trials[i], target)) {
                    return i;
                }
            }
            return -1;
        }
    }
}

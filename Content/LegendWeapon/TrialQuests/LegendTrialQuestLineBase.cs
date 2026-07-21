using CalamityOverhaul.Content.EntrustManager;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
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
        protected virtual LegendData GetLegendData(Player player) => null;

        private static LocalizedText trackerBlockedText;
        /// <summary>委托受阻提示</summary>
        protected static LocalizedText TrackerBlockedText
            => trackerBlockedText ??= Language.GetOrRegister("Mods.CalamityOverhaul.Legend.TrialBlockedHint",
                static () => "需要启用相应内容后才能开始本试炼");

        public override void PostUpdateEverything() {
            if (Main.dedServ || Main.gameMenu) {
                return;
            }

            var manager = QuestManagerUI.Instance;
            if (manager == null || Trials == null) {
                return;
            }

            bool allowCreate = CanCreateEntries(Main.LocalPlayer);
            //完成态=已确认进度∪世界实时击杀，只读；落盘仅 PerformUpgrade
            LegendData data = GetLegendData(Main.LocalPlayer);
            Func<LegendTrialDefinition, bool> isCompleted = BuildCompletionCheck(data);

            IReadOnlyList<LegendTrialDefinition> availableTrials = LegendTrialRouteResolver.GetAvailableTrials(Trials);
            int currentLevel = LegendTrialRouteResolver.GetSequentialLevel(Trials, isCompleted);

            //无可用下一关时补受阻关，避免试炼号悬空
            LegendTrialDefinition blockedFrontier = currentLevel >= availableTrials.Count
                ? FindBlockedFrontier(isCompleted)
                : null;

            for (int i = 0; i < LegacyTrialCount; i++) {
                manager.UnregisterQuest(KeyPrefix + i);
            }

            for (int i = 0; i < Trials.Count; i++) {
                LegendTrialDefinition trial = Trials[i];
                if (blockedFrontier != null && ReferenceEquals(trial, blockedFrontier)) {
                    SyncBlockedFrontier(manager, trial, i, Trials.Count, allowCreate);
                    continue;
                }

                int routeIndex = IndexOfTrial(availableTrials, trial);
                SyncTrial(manager, trial, routeIndex, currentLevel, allowCreate, availableTrials.Count, isCompleted);
            }
        }

        private void SyncTrial(QuestManagerUI manager, LegendTrialDefinition trial, int routeIndex, int currentLevel
            , bool allowCreate, int routeCount, Func<LegendTrialDefinition, bool> isCompleted) {
            string key = GetEntryKey(trial);
            if (trial == null || routeIndex < 0 || routeIndex > currentLevel) {
                manager.UnregisterQuest(key);
                return;
            }

            bool isDone = routeIndex < currentLevel || isCompleted(trial);
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

        /// <summary>未加载内容挡住的下一关，同步为受阻委托</summary>
        private void SyncBlockedFrontier(QuestManagerUI manager, LegendTrialDefinition trial, int originalIndex, int routeCount, bool allowCreate) {
            string key = GetEntryKey(trial);
            var entry = manager.GetEntry(key) as LegendTrialQuestEntry;
            if (entry == null) {
                if (!allowCreate) {
                    return;
                }
                entry = CreateTrialEntry(trial, originalIndex, routeCount);
                ApplyBlockedState(entry);
                manager.RegisterQuest(entry);
                return;
            }

            ApplyBlockedState(entry);
            if (entry.Status == QuestEntryStatus.Completed) {
                manager.SetEntryStatus(key, QuestEntryStatus.Active, 0f);
            }
        }

        private static void ApplyBlockedState(LegendTrialQuestEntry entry) {
            entry.Blocked = true;
            LocalizedText hint = TrackerBlockedText;
            entry.BlockedHint = hint;
            if (hint != null) {
                //描述换成受阻提示
                entry.SummaryText = hint;
            }
        }

        private LegendTrialQuestEntry EnsureTrialEntry(QuestManagerUI manager, LegendTrialDefinition trial, int routeIndex, int routeCount, bool completed = false, bool allowCreate = true) {
            string key = GetEntryKey(trial);
            var entry = manager.GetEntry(key) as LegendTrialQuestEntry;
            if (entry != null) {
                //曾受阻则复位
                if (entry.Blocked) {
                    entry.Blocked = false;
                    entry.BlockedHint = null;
                    entry.SummaryText = trial.Summary;
                }
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

        private static Func<LegendTrialDefinition, bool> BuildCompletionCheck(LegendData data) {
            if (data != null) {
                return data.IsTrialCompleted;
            }
            return static trial => trial?.IsCompleted == true;
        }

        /// <summary>可用试炼全完成后，定位进度后首关不可用的试炼</summary>
        private LegendTrialDefinition FindBlockedFrontier(Func<LegendTrialDefinition, bool> isCompleted) {
            int lastCompleted = -1;
            for (int i = 0; i < Trials.Count; i++) {
                if (Trials[i] != null && isCompleted(Trials[i])) {
                    lastCompleted = i;
                }
            }

            for (int i = lastCompleted + 1; i < Trials.Count; i++) {
                LegendTrialDefinition trial = Trials[i];
                if (trial == null || isCompleted(trial)) {
                    continue;
                }
                //进度后首关，不可用则受阻
                return trial.IsAvailable ? null : trial;
            }
            return null;
        }

        protected static LegendData FindLegendData(Player player, int itemType) {
            if (player == null || itemType <= ItemID.None) {
                return null;
            }

            foreach (Item item in player.inventory) {
                if (item.Alives() && item.type == itemType) {
                    return item.CWR()?.LegendData;
                }
            }
            return null;
        }

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

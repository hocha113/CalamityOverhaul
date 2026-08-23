using CalamityOverhaul.Content.LegendWeapon.KikasaLegend;
using CalamityOverhaul.Content.LegendWeapon.TrialQuests;
using CalamityOverhaul.Content.Narrative;
using CalamityOverhaul.Content.Narrative.Common;
using CalamityOverhaul.Content.Narrative.Data;
using InnoVault.Narrative.Core;
using InnoVault.Narrative.Runtime;
using System;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.Scenarios.Shenyo.Gifts
{
    internal abstract class ShenyoBossGiftNarrative : StoryScenario
    {
        public string GiftId
            => ShenyoGiftCatalog.TryGet(GetType(), out ShenyoGiftEntry entry) ? entry.Id : string.Empty;

        /// <summary>默认直接读沉宴试炼线自己的完成位，天然覆盖二选一/多首领合并，通常无需子类重写</summary>
        protected virtual bool CanSpawned()
            => ShenyoGiftCatalog.TryGet(GetType(), out ShenyoGiftEntry entry)
                && LegendTrialRouteCatalog.KikasaProgression[entry.Order].IsCompleted;

        protected abstract bool IsGiftCompleted();
        protected abstract void MarkGiftCompleted();
        protected virtual bool AdditionalConditions(Player player) => true;

        internal bool ShouldSpawn() => CanSpawned();
        internal bool CheckGiftCompleted() => IsGiftCompleted();
        internal bool MeetsAdditionalConditions(Player player) => AdditionalConditions(player);
        internal void CompleteGift() => MarkGiftCompleted();

        protected override void OnStarted() => ShenyoNarrativePortrait.Show();

        protected override void OnCompleted() => ShenyoNarrativePortrait.Hide();

        protected static Action PortraitFace(ShenyoFullBodyPortrait.Face face)
            => ShenyoNarrativePortrait.FaceEnter(face);

        protected override NarrativePolicy ConfigurePolicy() => null;
    }

    /// <summary>
    /// 本次击杀登记：全程本地、不落盘，与真夜礼物线同一套契约。<br/>
    /// 唯一权威是场景各自的完成位，开演即写
    /// </summary>
    internal static class ShenyoGiftNarrativeTracker
    {
        private const string FinaleGiftId = "BossRush";

        private static readonly Dictionary<string, ShenyoBossGiftNarrative> scenariosById = new(StringComparer.Ordinal);
        private static readonly Dictionary<int, List<ShenyoGiftEntry>> byBossId = [];
        private static readonly HashSet<string> spawned = new(StringComparer.Ordinal);
        //二选一分支取词：记录某个多首领礼物最近一次是被哪个NPC id触发的
        private static readonly Dictionary<string, int> lastDefeatedById = new(StringComparer.Ordinal);
        private static bool wasFinaleDowned;

        public static int LastDefeatedBossId(string giftId)
            => lastDefeatedById.TryGetValue(giftId, out int bossId) ? bossId : 0;

        public static void ResetWorldState() {
            spawned.Clear();
            lastDefeatedById.Clear();
            wasFinaleDowned = CWRRef.Has && CWRRef.GetDownedBossRush();
            RegisterAll();
        }

        private static void RegisterAll() {
            scenariosById.Clear();
            byBossId.Clear();
            foreach (NarrativeScenario scenario in NarrativeScenario.All) {
                if (scenario is not ShenyoBossGiftNarrative gift
                    || !ShenyoGiftCatalog.TryGet(gift.GetType(), out ShenyoGiftEntry entry)) {
                    continue;
                }
                if (!scenariosById.TryAdd(entry.Id, gift)) {
                    CWRMod.Instance.Logger.Error($"[ShenyoGift] duplicate scenario for Id '{entry.Id}'");
                    continue;
                }

                int[] targets = entry.TargetBossIds;
                for (int i = 0; i < targets.Length; i++) {
                    if (targets[i] <= 0) {
                        continue;
                    }
                    if (!byBossId.TryGetValue(targets[i], out List<ShenyoGiftEntry> list)) {
                        list = [];
                        byBossId[targets[i]] = list;
                    }
                    if (!list.Contains(entry)) {
                        list.Add(entry);
                    }
                }
            }

            if (scenariosById.Count != ShenyoGiftCatalog.GiftCount) {
                CWRMod.Instance.Logger.Error(
                    $"[ShenyoGift] catalog/scenario mismatch: {scenariosById.Count}/{ShenyoGiftCatalog.GiftCount}");
            }
        }

        public static void NotifyBossDefeated(int bossId) {
            if (!byBossId.TryGetValue(bossId, out List<ShenyoGiftEntry> gifts)) {
                return;
            }

            for (int i = 0; i < gifts.Count; i++) {
                if (gifts[i].TargetBossIds.Length > 1) {
                    lastDefeatedById[gifts[i].Id] = bossId;
                }
                if (scenariosById.TryGetValue(gifts[i].Id, out ShenyoBossGiftNarrative gift) && gift.ShouldSpawn()) {
                    spawned.Add(gifts[i].Id);
                }
            }
        }

        public static void Tick() {
            if (scenariosById.Count == 0) {
                RegisterAll();
            }

            TickFinaleEdge();
            TickLocalNarrative();
        }

        //BossRush本身不是击杀事件，只能靠边沿检测；始源妖龙那条腿走NotifyBossDefeated正常路径
        private static void TickFinaleEdge() {
            if (!CWRRef.Has || CWRRef.GetBossRushActive()) {
                return;
            }

            bool downed = CWRRef.GetDownedBossRush();
            if (downed && !wasFinaleDowned
                && scenariosById.TryGetValue(FinaleGiftId, out ShenyoBossGiftNarrative finale)
                && finale.ShouldSpawn()) {
                spawned.Add(FinaleGiftId);
            }
            wasFinaleDowned = downed;
        }

        private static void TickLocalNarrative() {
            Player player = Main.LocalPlayer;
            if (player == null || !player.active || !player.HasItem(KikasaOverride.ID)
                || !ShenyoStorySync.PostFirstMetIsComplete) {
                return;
            }
            if (CWRWorld.HasBoss || CWRWorld.BossRush || NarrativeTriggerGate.IsBusy) {
                return;
            }
            if (!TryPickNext(player, out ShenyoBossGiftNarrative gift, out string giftId)) {
                return;
            }

            StoryPlayer storyPlayer = player.GetModPlayer<StoryPlayer>();
            if (storyPlayer.ShenyoGiftDelayKey != giftId) {
                storyPlayer.ShenyoGiftDelayKey = giftId;
                storyPlayer.ShenyoGiftDelayTicks = 60 * Main.rand.Next(2, 4);
                return;
            }
            if (storyPlayer.ShenyoGiftDelayTicks > 0) {
                storyPlayer.ShenyoGiftDelayTicks--;
                return;
            }

            if (NarrativeRunner.Begin(gift)) {
                //开演即落完成位，这场戏不再有第二次
                gift.CompleteGift();
                spawned.Remove(giftId);
                storyPlayer.ShenyoGiftDelayKey = null;
                storyPlayer.ShenyoGiftDelayTicks = 0;
            }
            else {
                storyPlayer.ShenyoGiftDelayTicks = 30;
            }
        }

        /// <summary>按名册次序取第一个能演的，暂时不能演的那项不挡后面</summary>
        private static bool TryPickNext(Player player, out ShenyoBossGiftNarrative gift, out string giftId) {
            foreach (ShenyoGiftEntry entry in ShenyoGiftCatalog.All) {
                if (!spawned.Contains(entry.Id)
                    || !scenariosById.TryGetValue(entry.Id, out ShenyoBossGiftNarrative candidate)) {
                    continue;
                }
                if (candidate.CheckGiftCompleted()) {
                    spawned.Remove(entry.Id);
                    continue;
                }
                if (!candidate.MeetsAdditionalConditions(player)
                    || NarrativeRunner.IsScenarioActiveOrPending(candidate.Key)) {
                    continue;
                }

                gift = candidate;
                giftId = entry.Id;
                return true;
            }

            gift = null;
            giftId = null;
            return false;
        }
    }

    internal sealed class ShenyoGiftBossKillNPC : DeathTrackingNPC
    {
        public override bool AppliesToEntity(NPC entity, bool lateInstantiation) => true;

        public override void OnNPCDeath(NPC npc) {
            if (Main.dedServ) {
                return;
            }

            ShenyoGiftNarrativeTracker.NotifyBossDefeated(npc.type);
        }
    }
}

using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend;
using CalamityOverhaul.Content.Narrative;
using CalamityOverhaul.Content.Narrative.Common;
using InnoVault.Narrative.Core;
using InnoVault.Narrative.Runtime;
using System;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.Scenarios.Himayo.Gifts
{
    internal abstract class HimayoBossGiftNarrative : StoryScenario
    {
        /// <summary>主 Boss type；多目标请覆写 <see cref="TargetBossIds"/></summary>
        public virtual int TargetBossId => 0;

        public virtual int[] TargetBossIds
            => TargetBossId > 0 ? [TargetBossId] : [];

        /// <summary>试炼 021：靠 Boss Rush 完成边沿，不挂 NPC type</summary>
        public virtual bool IsBossRushGift => false;

        protected virtual bool CanSpawned() => true;

        protected abstract bool IsGiftCompleted();
        protected abstract void MarkGiftCompleted();
        protected virtual bool AdditionalConditions(Player player) => true;

        internal bool ShouldSpawn() => CanSpawned();
        internal bool CheckGiftCompleted() => IsGiftCompleted();
        internal bool MeetsAdditionalConditions(Player player) => AdditionalConditions(player);
        internal void CompleteGift() => MarkGiftCompleted();

        protected override void OnStarted() => HimayoNarrativePortrait.Show();

        protected override void OnCompleted() => HimayoNarrativePortrait.Hide();

        protected static Action PortraitFace(HimayoFullBodyPortrait.Face face)
            => HimayoNarrativePortrait.FaceEnter(face);

        protected override NarrativePolicy ConfigurePolicy() => null;
    }

    internal static class HimayoGiftNarrativeTracker
    {
        private static readonly Dictionary<HimayoBossGiftNarrative, bool> spawned = [];
        private static readonly Dictionary<int, List<HimayoBossGiftNarrative>> byBossId = [];
        private static readonly Dictionary<string, int> pendingTimers = new(StringComparer.Ordinal);
        private static readonly List<HimayoBossGiftNarrative> bossRushGifts = [];
        private static bool wasDownedBossRush;

        public static void ResetWorldState() {
            spawned.Clear();
            byBossId.Clear();
            pendingTimers.Clear();
            bossRushGifts.Clear();
            wasDownedBossRush = CWRRef.Has && CWRRef.GetDownedBossRush();
            RegisterAll();
        }

        private static void RegisterAll() {
            foreach (NarrativeScenario scenario in NarrativeScenario.All) {
                if (scenario is not HimayoBossGiftNarrative gift) {
                    continue;
                }

                spawned[gift] = false;

                if (gift.IsBossRushGift) {
                    if (!bossRushGifts.Contains(gift)) {
                        bossRushGifts.Add(gift);
                    }
                    continue;
                }

                int[] ids = gift.TargetBossIds;
                for (int i = 0; i < ids.Length; i++) {
                    int bossId = ids[i];
                    if (bossId <= 0) {
                        continue;
                    }

                    if (!byBossId.TryGetValue(bossId, out List<HimayoBossGiftNarrative> list)) {
                        list = [];
                        byBossId[bossId] = list;
                    }

                    if (!list.Contains(gift)) {
                        list.Add(gift);
                    }
                }
            }
        }

        public static void NotifyBossDefeated(int bossId) {
            if (CWRRef.GetBossRushActive() || !byBossId.TryGetValue(bossId, out List<HimayoBossGiftNarrative> gifts)) {
                return;
            }

            LastDefeatedBossId = bossId;

            for (int i = 0; i < gifts.Count; i++) {
                HimayoBossGiftNarrative gift = gifts[i];
                if (gift.ShouldSpawn()) {
                    spawned[gift] = true;
                }
            }
        }

        /// <summary>最近一次触发礼物登记的 Boss type，供双目标场分支台词</summary>
        public static int LastDefeatedBossId { get; private set; }

        public static void NotifyBossRushCleared() {
            if (CWRRef.GetBossRushActive()) {
                return;
            }

            for (int i = 0; i < bossRushGifts.Count; i++) {
                HimayoBossGiftNarrative gift = bossRushGifts[i];
                if (gift.ShouldSpawn()) {
                    spawned[gift] = true;
                }
            }
        }

        public static void Tick() {
            if (spawned.Count == 0) {
                RegisterAll();
            }

            TickBossRushEdge();

            Player player = Main.LocalPlayer;
            if (!player.HasItem(OnikiriOverride.ID) || !HimayoStorySync.PostFirstMetIsComplete) {
                return;
            }

            if (CWRWorld.HasBoss || CWRWorld.BossRush || NarrativeTriggerGate.IsBusy) {
                return;
            }

            foreach (KeyValuePair<HimayoBossGiftNarrative, bool> pair in spawned) {
                HimayoBossGiftNarrative gift = pair.Key;
                if (!pair.Value || gift.CheckGiftCompleted() || !gift.MeetsAdditionalConditions(player)) {
                    continue;
                }

                if (NarrativeRunner.IsScenarioActiveOrPending(gift.Key)) {
                    continue;
                }

                if (!pendingTimers.TryGetValue(gift.Key, out int timer)) {
                    pendingTimers[gift.Key] = 60 * Main.rand.Next(2, 4);
                    continue;
                }

                if (timer > 0) {
                    pendingTimers[gift.Key] = timer - 1;
                    continue;
                }

                if (NarrativeRunner.Begin(gift)) {
                    gift.CompleteGift();
                    pendingTimers.Remove(gift.Key);
                    spawned[gift] = false;
                }
                else {
                    pendingTimers[gift.Key] = 30;
                }
            }
        }

        private static void TickBossRushEdge() {
            if (!CWRRef.Has) {
                return;
            }

            bool downed = CWRRef.GetDownedBossRush();
            if (downed && !wasDownedBossRush) {
                NotifyBossRushCleared();
            }

            wasDownedBossRush = downed;
        }
    }

    internal sealed class HimayoGiftBossKillNPC : DeathTrackingNPC
    {
        public override bool AppliesToEntity(NPC entity, bool lateInstantiation) => true;

        public override void OnNPCDeath(NPC npc) {
            if (Main.dedServ) {
                return;
            }

            HimayoGiftNarrativeTracker.NotifyBossDefeated(npc.type);
        }
    }
}

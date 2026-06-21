using CalamityOverhaul.Content.LegendWeapon.HalibutLegend;
using CalamityOverhaul.Content.Narrative;
using CalamityOverhaul.Content.Scenarios.Helen;
using InnoVault.Narrative.Core;
using InnoVault.Narrative.Runtime;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Helen.Gifts
{
    internal abstract class HelenBossGiftNarrative : NarrativeScenario
    {
        public abstract int TargetBossId { get; }
        protected virtual bool CanSpawned() => true;

        protected abstract bool IsGiftCompleted();
        protected abstract void MarkGiftCompleted();
        protected virtual bool AdditionalConditions(Player player) => true;

        internal bool ShouldSpawn() => CanSpawned();
        internal bool CheckGiftCompleted() => IsGiftCompleted();
        internal bool MeetsAdditionalConditions(Player player) => AdditionalConditions(player);
        internal void CompleteGift() => MarkGiftCompleted();

        protected override NarrativePolicy ConfigurePolicy() => null;
    }

    internal static class HelenGiftNarrativeTracker
    {
        private static readonly Dictionary<HelenBossGiftNarrative, bool> spawned = [];
        private static readonly Dictionary<int, List<HelenBossGiftNarrative>> byBossId = [];
        private static readonly Dictionary<string, int> pendingTimers = new(StringComparer.Ordinal);

        public static void ResetWorldState() {
            spawned.Clear();
            byBossId.Clear();
            pendingTimers.Clear();
            RegisterAll();
        }

        private static void RegisterAll() {
            foreach (NarrativeScenario scenario in NarrativeScenario.All) {
                if (scenario is not HelenBossGiftNarrative gift) {
                    continue;
                }

                spawned[gift] = false;
                if (gift.TargetBossId <= 0) {
                    continue;
                }

                if (!byBossId.TryGetValue(gift.TargetBossId, out List<HelenBossGiftNarrative> list)) {
                    list = [];
                    byBossId[gift.TargetBossId] = list;
                }

                if (!list.Contains(gift)) {
                    list.Add(gift);
                }
            }
        }

        public static void NotifyBossDefeated(int bossId) {
            if (CWRRef.GetBossRushActive() || !byBossId.TryGetValue(bossId, out List<HelenBossGiftNarrative> gifts)) {
                return;
            }

            for (int i = 0; i < gifts.Count; i++) {
                HelenBossGiftNarrative gift = gifts[i];
                if (gift.ShouldSpawn()) {
                    spawned[gift] = true;
                }
            }
        }

        public static void Tick() {
            if (spawned.Count == 0) {
                RegisterAll();
            }

            Player player = Main.LocalPlayer;
            if (!player.TryGetOverride(out HalibutPlayer halibutPlayer)
                || !halibutPlayer.HeldHalibut
                || !HalibutStorySync.ReadHalibut(d => d.FirstMet, d => d.FirstMet)) {
                return;
            }

            if (CWRWorld.HasBoss || CWRWorld.BossRush || NarrativeTriggerGate.IsBusy) {
                return;
            }

            foreach (KeyValuePair<HelenBossGiftNarrative, bool> pair in spawned) {
                HelenBossGiftNarrative gift = pair.Key;
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
    }

    internal sealed class HelenGiftBossKillNPC : GlobalNPC
    {
        public override void OnKill(NPC npc) {
            HelenGiftNarrativeTracker.NotifyBossDefeated(npc.type);
        }
    }
}

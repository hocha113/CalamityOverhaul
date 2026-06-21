using CalamityOverhaul.Content.LegendWeapon.SHPCLegend;
using CalamityOverhaul.Content.Scenarios.Shepel;
using InnoVault.Narrative.Core;
using InnoVault.Narrative.Runtime;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Shepel.Gifts
{
    internal abstract class ShepelBossGiftNarrative : NarrativeScenario
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

        protected override void OnStarted() => ShepelNarrativePortrait.Show();

        protected override void OnCompleted() => ShepelNarrativePortrait.Hide();

        protected static Action PortraitFace(ShepelFullBodyPortrait.Face face)
            => ShepelNarrativePortrait.FaceEnter(face);

        protected static Action PortraitSmirk => PortraitFace(ShepelFullBodyPortrait.Face.Smirk);
        protected static Action PortraitHappy => PortraitFace(ShepelFullBodyPortrait.Face.Happy);
        protected static Action PortraitBlank => PortraitFace(ShepelFullBodyPortrait.Face.Blank);

        protected override NarrativePolicy ConfigurePolicy() => null;
    }

    internal static class ShepelGiftNarrativeTracker
    {
        private static readonly Dictionary<ShepelBossGiftNarrative, bool> spawned = [];
        private static readonly Dictionary<int, List<ShepelBossGiftNarrative>> byBossId = [];
        private static readonly Dictionary<string, int> pendingTimers = new(StringComparer.Ordinal);

        public static void ResetWorldState() {
            spawned.Clear();
            byBossId.Clear();
            pendingTimers.Clear();
            RegisterAll();
        }

        private static void RegisterAll() {
            foreach (NarrativeScenario scenario in NarrativeScenario.All) {
                if (scenario is not ShepelBossGiftNarrative gift) {
                    continue;
                }

                spawned[gift] = false;
                if (gift.TargetBossId <= 0) {
                    continue;
                }

                if (!byBossId.TryGetValue(gift.TargetBossId, out List<ShepelBossGiftNarrative> list)) {
                    list = [];
                    byBossId[gift.TargetBossId] = list;
                }

                if (!list.Contains(gift)) {
                    list.Add(gift);
                }
            }
        }

        public static void NotifyBossDefeated(int bossId) {
            if (CWRRef.GetBossRushActive() || !byBossId.TryGetValue(bossId, out List<ShepelBossGiftNarrative> gifts)) {
                return;
            }

            for (int i = 0; i < gifts.Count; i++) {
                ShepelBossGiftNarrative gift = gifts[i];
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
            if (!player.HasItem(SHPCOverride.ID)
                || !ShepelStorySync.ReadShepel(d => d.FirstSHPCObtained, d => d.FirstSHPCObtained)) {
                return;
            }

            if (CWRWorld.HasBoss || CWRWorld.BossRush || NarrativeRunner.IsBusy) {
                return;
            }

            foreach (KeyValuePair<ShepelBossGiftNarrative, bool> pair in spawned) {
                ShepelBossGiftNarrative gift = pair.Key;
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

    internal sealed class ShepelGiftBossKillNPC : GlobalNPC
    {
        public override void OnKill(NPC npc) {
            ShepelGiftNarrativeTracker.NotifyBossDefeated(npc.type);
        }
    }
}

using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend;
using CalamityOverhaul.Content.Narrative;
using CalamityOverhaul.Content.Narrative.Common;
using CalamityOverhaul.Content.Narrative.Data;
using InnoVault.Narrative.Core;
using InnoVault.Narrative.Runtime;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.Scenarios.Himayo.Gifts
{
    internal abstract class HimayoBossGiftNarrative : StoryScenario
    {
        public virtual int TargetBossId => 0;

        public virtual int[] TargetBossIds
            => TargetBossId > 0 ? [TargetBossId] : [];

        public virtual bool IsBossRushGift => false;

        public string GiftKey
            => HimayoGiftCatalog.TryGet(GetType(), out HimayoGiftEntry entry) ? entry.MeiKey : string.Empty;

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

    /// <summary>
    /// 与 Helen/Shepel 一致：仅在实际击杀登记入队，不用世界 downed 旗标自动补发。
    /// </summary>
    internal static class HimayoGiftNarrativeTracker
    {
        private static readonly Dictionary<string, HimayoBossGiftNarrative> scenariosByGiftKey = new(StringComparer.Ordinal);
        private static bool wasDownedBossRush;

        public static int LastDefeatedBossId {
            get {
                Player player = Main.LocalPlayer;
                return Main.netMode != NetmodeID.Server && player?.active == true
                    ? player.GetModPlayer<StoryPlayer>().HimayoLastDefeatedBossId
                    : 0;
            }
        }

        public static void ResetWorldState() {
            scenariosByGiftKey.Clear();
            wasDownedBossRush = CWRRef.Has && CWRRef.GetDownedBossRush();
            RegisterAll();
        }

        private static void RegisterAll() {
            foreach (NarrativeScenario scenario in NarrativeScenario.All) {
                if (scenario is not HimayoBossGiftNarrative gift
                    || !HimayoGiftCatalog.TryGet(gift.GetType(), out HimayoGiftEntry entry)) {
                    continue;
                }
                if (!scenariosByGiftKey.TryAdd(entry.MeiKey, gift)) {
                    CWRMod.Instance.Logger.Error($"[HimayoGift] duplicate scenario for Key '{entry.MeiKey}'");
                }
            }

            if (scenariosByGiftKey.Count != HimayoGiftCatalog.GiftCount) {
                CWRMod.Instance.Logger.Error(
                    $"[HimayoGift] catalog/scenario mismatch: {scenariosByGiftKey.Count}/{HimayoGiftCatalog.GiftCount}");
            }
        }

        public static void NotifyBossDefeated(int bossId) {
            if (CWRRef.GetBossRushActive() || bossId <= 0) {
                return;
            }

            Player player = Main.LocalPlayer;
            if (player?.active != true) {
                return;
            }

            player.GetModPlayer<StoryPlayer>().HimayoLastDefeatedBossId = bossId;
            EnqueueMatchingBossGifts(player, bossId);
        }

        public static void Tick() {
            if (scenariosByGiftKey.Count == 0) {
                RegisterAll();
            }

            if (Main.netMode == NetmodeID.Server) {
                return;
            }

            TickBossRushEdge();
            TickLocalNarrative();
        }

        private static void TickBossRushEdge() {
            if (!CWRRef.Has || CWRRef.GetBossRushActive()) {
                return;
            }

            bool downed = CWRRef.GetDownedBossRush();
            if (downed && !wasDownedBossRush) {
                Player player = Main.LocalPlayer;
                if (player?.active == true) {
                    EnqueueBossRushGift(player);
                }
            }
            wasDownedBossRush = downed;
        }

        private static void EnqueueMatchingBossGifts(Player player, int bossId) {
            IReadOnlyList<HimayoGiftEntry> all = HimayoGiftCatalog.All;
            for (int i = 0; i < all.Count; i++) {
                HimayoGiftEntry entry = all[i];
                if (!scenariosByGiftKey.TryGetValue(entry.MeiKey, out HimayoBossGiftNarrative gift)
                    || gift.IsBossRushGift || !gift.ShouldSpawn()) {
                    continue;
                }

                int[] ids = entry.TargetBossIds;
                bool matched = false;
                for (int j = 0; j < ids.Length; j++) {
                    if (ids[j] > 0 && ids[j] == bossId) {
                        matched = true;
                        break;
                    }
                }
                if (!matched) {
                    continue;
                }

                HimayoStorySync.TryEnqueueGift(player, entry.MeiKey);
            }
        }

        private static void EnqueueBossRushGift(Player player) {
            IReadOnlyList<HimayoGiftEntry> all = HimayoGiftCatalog.All;
            for (int i = 0; i < all.Count; i++) {
                HimayoGiftEntry entry = all[i];
                if (!scenariosByGiftKey.TryGetValue(entry.MeiKey, out HimayoBossGiftNarrative gift)
                    || !gift.IsBossRushGift || !gift.ShouldSpawn()) {
                    continue;
                }
                HimayoStorySync.TryEnqueueGift(player, entry.MeiKey);
            }
        }

        private static void TickLocalNarrative() {
            Player player = Main.LocalPlayer;
            if (player == null || !player.active || !player.HasItem(OnikiriOverride.ID)
                || !HimayoStorySync.PostFirstMetIsComplete) {
                return;
            }
            if (CWRWorld.HasBoss || CWRWorld.BossRush || NarrativeTriggerGate.IsBusy) {
                return;
            }
            if (!HimayoStorySync.TryGetNextPending(player, out HimayoGiftEntry entry)
                || !scenariosByGiftKey.TryGetValue(entry.MeiKey, out HimayoBossGiftNarrative gift)
                || gift.CheckGiftCompleted() || !gift.MeetsAdditionalConditions(player)
                || NarrativeRunner.IsScenarioActiveOrPending(gift.Key)) {
                return;
            }

            StoryPlayer storyPlayer = player.GetModPlayer<StoryPlayer>();
            if (storyPlayer.HimayoGiftDelayKey != entry.MeiKey) {
                storyPlayer.HimayoGiftDelayKey = entry.MeiKey;
                storyPlayer.HimayoGiftDelayTicks = 60 * Main.rand.Next(2, 4);
                return;
            }
            if (storyPlayer.HimayoGiftDelayTicks > 0) {
                storyPlayer.HimayoGiftDelayTicks--;
                return;
            }
            if (!HimayoStorySync.CanReceiveGift(player, entry.MeiKey)) {
                storyPlayer.HimayoGiftDelayTicks = 30;
                return;
            }

            if (NarrativeRunner.Begin(gift)) {
                storyPlayer.HimayoGiftDelayKey = null;
                storyPlayer.HimayoGiftDelayTicks = 0;
            }
            else {
                storyPlayer.HimayoGiftDelayTicks = 30;
            }
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

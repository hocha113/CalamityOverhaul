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
using Terraria.ModLoader;

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

    /// <summary>Reconciles persistent world flags into each player's durable pending queue.</summary>
    internal static class HimayoGiftNarrativeTracker
    {
        private const int ReconcileDelayTicks = 2;
        private static readonly Dictionary<string, HimayoBossGiftNarrative> scenariosByGiftKey = new(StringComparer.Ordinal);
        private static readonly HashSet<int> serverEntitledPlayers = [];
        private static bool wasDownedBossRush;
        private static int serverReconcileDelay = -1;
        private static int localReconcileDelay = -1;
        private static int serverPendingBossId;
        private static int localPendingBossId;

        public static int LastDefeatedBossId {
            get {
                Player player = Main.LocalPlayer;
                return Main.netMode != NetmodeID.Server && player?.active == true
                    ? HimayoStorySync.GetEvilBossGiftBossId(player)
                    : 0;
            }
        }

        public static void ResetWorldState() {
            scenariosByGiftKey.Clear();
            serverEntitledPlayers.Clear();
            wasDownedBossRush = CWRRef.Has && CWRRef.GetDownedBossRush();
            serverReconcileDelay = Main.netMode == NetmodeID.Server ? ReconcileDelayTicks : -1;
            localReconcileDelay = Main.netMode == NetmodeID.SinglePlayer ? ReconcileDelayTicks : -1;
            serverPendingBossId = 0;
            localPendingBossId = 0;
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
            if (bossId <= 0 || !HimayoGiftCatalog.IsTargetBoss(bossId)) {
                return;
            }
            if (CWRRef.GetBossRushActive()) {
                return;
            }

            if (Main.netMode == NetmodeID.Server) {
                ScheduleServerReconcile(bossId);
                return;
            }

            Player player = Main.LocalPlayer;
            if (player?.active != true) {
                return;
            }

            localPendingBossId = bossId;
            localReconcileDelay = Math.Max(localReconcileDelay, ReconcileDelayTicks);
        }

        public static void Tick() {
            if (scenariosByGiftKey.Count == 0) {
                RegisterAll();
            }

            if (Main.netMode == NetmodeID.Server) {
                TickBossRushEdge();
                TickServerEntitlements();
                return;
            }

            if (Main.netMode == NetmodeID.SinglePlayer) {
                TickLocalEntitlements();
                TickBossRushEdge();
            }
            TickLocalNarrative();
        }

        private static void TickBossRushEdge() {
            if (!CWRRef.Has || CWRRef.GetBossRushActive()) {
                return;
            }

            bool downed = CWRRef.GetDownedBossRush();
            if (downed && !wasDownedBossRush) {
                if (Main.netMode == NetmodeID.Server) {
                    ScheduleServerReconcile(0);
                }
                else {
                    localReconcileDelay = Math.Max(localReconcileDelay, ReconcileDelayTicks);
                }
            }
            wasDownedBossRush = downed;
        }

        private static List<string> CollectWorldEntitlements() {
            List<string> keys = [];
            IReadOnlyList<HimayoGiftEntry> all = HimayoGiftCatalog.All;
            for (int i = 0; i < all.Count; i++) {
                HimayoGiftEntry entry = all[i];
                if (!scenariosByGiftKey.TryGetValue(entry.MeiKey, out HimayoBossGiftNarrative gift)
                    || !gift.ShouldSpawn() || !HimayoGiftCatalog.IsWorldConditionMet(entry)) {
                    continue;
                }
                keys.Add(entry.MeiKey);
            }
            return keys;
        }

        private static bool ReceiveLocalEntitlements(int lastDefeatedBossId) {
            Player player = Main.LocalPlayer;
            if (player?.active != true) {
                return false;
            }
            HimayoStorySync.ReceiveGiftEntitlements(player, CollectWorldEntitlements(), lastDefeatedBossId);
            return true;
        }

        private static void TickLocalEntitlements() {
            if (localReconcileDelay < 0) {
                return;
            }
            if (localReconcileDelay > 0) {
                localReconcileDelay--;
                return;
            }
            if (ReceiveLocalEntitlements(localPendingBossId)) {
                localPendingBossId = 0;
                localReconcileDelay = -1;
            }
        }

        private static void TickServerEntitlements() {
            serverEntitledPlayers.RemoveWhere(index => index < 0 || index >= Main.maxPlayers || !Main.player[index].active);
            if (serverReconcileDelay >= 0) {
                if (serverReconcileDelay > 0) {
                    serverReconcileDelay--;
                    return;
                }
                int defeatedBossId = serverPendingBossId;
                serverPendingBossId = 0;
                serverReconcileDelay = -1;
                for (int i = 0; i < Main.maxPlayers; i++) {
                    if (Main.player[i].active) {
                        SendEntitlements(Main.player[i], defeatedBossId);
                        serverEntitledPlayers.Add(i);
                    }
                }
                return;
            }

            for (int i = 0; i < Main.maxPlayers; i++) {
                if (Main.player[i].active && serverEntitledPlayers.Add(i)) {
                    SendEntitlements(Main.player[i], 0);
                }
            }
        }

        private static void ScheduleServerReconcile(int bossId) {
            serverReconcileDelay = Math.Max(serverReconcileDelay, ReconcileDelayTicks);
            if (bossId > 0) {
                serverPendingBossId = bossId;
            }
        }

        private static void SendEntitlements(Player player, int lastDefeatedBossId) {
            if (player?.active != true) {
                return;
            }

            List<string> keys = CollectWorldEntitlements();
            ModPacket packet = CWRMod.Instance.GetPacket();
            packet.Write((byte)CWRMessageType.HimayoGiftEntitlements);
            packet.Write(lastDefeatedBossId);
            packet.Write((byte)keys.Count);
            for (int i = 0; i < keys.Count; i++) {
                packet.Write(keys[i]);
            }
            packet.Send(player.whoAmI);
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
            if (Main.netMode == NetmodeID.MultiplayerClient) {
                return;
            }

            HimayoGiftNarrativeTracker.NotifyBossDefeated(npc.type);
        }
    }
}

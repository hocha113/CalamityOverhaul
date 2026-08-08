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
        //首领绑定只在 HimayoGiftCatalog 一处声明，场景侧不再重复
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
    /// 本次击杀登记：全程本地、不落盘，与海伦 / SHPC 同一套契约。<br/>
    /// 唯一权威是场景各自的完成位，开演即写；拓本发放是纯副作用，不参与判定
    /// </summary>
    internal static class HimayoGiftNarrativeTracker
    {
        private static readonly Dictionary<string, HimayoBossGiftNarrative> scenariosByGiftKey = new(StringComparer.Ordinal);
        private static readonly Dictionary<int, List<HimayoGiftEntry>> byBossId = [];
        private static readonly HashSet<string> spawned = new(StringComparer.Ordinal);
        private static bool wasDownedBossRush;
        private static int lastEvilBossId;

        /// <summary>邪恶首领分支取词：本次击杀优先，未记则按世界的邪恶属性</summary>
        public static int LastDefeatedBossId
            => lastEvilBossId == NPCID.EaterofWorldsHead || lastEvilBossId == NPCID.BrainofCthulhu
                ? lastEvilBossId
                : WorldGen.crimson ? NPCID.BrainofCthulhu : NPCID.EaterofWorldsHead;

        public static void ResetWorldState() {
            spawned.Clear();
            lastEvilBossId = 0;
            wasDownedBossRush = CWRRef.Has && CWRRef.GetDownedBossRush();
            RegisterAll();
        }

        private static void RegisterAll() {
            scenariosByGiftKey.Clear();
            byBossId.Clear();
            foreach (NarrativeScenario scenario in NarrativeScenario.All) {
                if (scenario is not HimayoBossGiftNarrative gift
                    || !HimayoGiftCatalog.TryGet(gift.GetType(), out HimayoGiftEntry entry)) {
                    continue;
                }
                if (!scenariosByGiftKey.TryAdd(entry.MeiKey, gift)) {
                    CWRMod.Instance.Logger.Error($"[HimayoGift] duplicate scenario for Key '{entry.MeiKey}'");
                    continue;
                }

                int[] targets = entry.TargetBossIds;
                for (int i = 0; i < targets.Length; i++) {
                    if (targets[i] <= 0) {
                        continue;
                    }
                    if (!byBossId.TryGetValue(targets[i], out List<HimayoGiftEntry> list)) {
                        list = [];
                        byBossId[targets[i]] = list;
                    }
                    if (!list.Contains(entry)) {
                        list.Add(entry);
                    }
                }
            }

            if (scenariosByGiftKey.Count != HimayoGiftCatalog.GiftCount) {
                CWRMod.Instance.Logger.Error(
                    $"[HimayoGift] catalog/scenario mismatch: {scenariosByGiftKey.Count}/{HimayoGiftCatalog.GiftCount}");
            }
        }

        public static void NotifyBossDefeated(int bossId) {
            if (CWRRef.GetBossRushActive() || !byBossId.TryGetValue(bossId, out List<HimayoGiftEntry> gifts)) {
                return;
            }
            if (bossId == NPCID.EaterofWorldsHead || bossId == NPCID.BrainofCthulhu) {
                lastEvilBossId = bossId;
            }

            for (int i = 0; i < gifts.Count; i++) {
                if (scenariosByGiftKey.TryGetValue(gifts[i].MeiKey, out HimayoBossGiftNarrative gift)
                    && !gift.IsBossRushGift && gift.ShouldSpawn()) {
                    spawned.Add(gifts[i].MeiKey);
                }
            }
        }

        public static void Tick() {
            if (scenariosByGiftKey.Count == 0) {
                RegisterAll();
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
                foreach (HimayoGiftEntry entry in HimayoGiftCatalog.All) {
                    if (scenariosByGiftKey.TryGetValue(entry.MeiKey, out HimayoBossGiftNarrative gift)
                        && gift.IsBossRushGift && gift.ShouldSpawn()) {
                        spawned.Add(entry.MeiKey);
                    }
                }
            }
            wasDownedBossRush = downed;
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
            if (!TryPickNext(player, out HimayoBossGiftNarrative gift, out string giftKey)) {
                return;
            }

            StoryPlayer storyPlayer = player.GetModPlayer<StoryPlayer>();
            if (storyPlayer.HimayoGiftDelayKey != giftKey) {
                storyPlayer.HimayoGiftDelayKey = giftKey;
                storyPlayer.HimayoGiftDelayTicks = 60 * Main.rand.Next(2, 4);
                return;
            }
            if (storyPlayer.HimayoGiftDelayTicks > 0) {
                storyPlayer.HimayoGiftDelayTicks--;
                return;
            }

            if (NarrativeRunner.Begin(gift)) {
                //开演即落完成位，这场戏不再有第二次
                gift.CompleteGift();
                spawned.Remove(giftKey);
                storyPlayer.HimayoGiftDelayKey = null;
                storyPlayer.HimayoGiftDelayTicks = 0;
            }
            else {
                storyPlayer.HimayoGiftDelayTicks = 30;
            }
        }

        /// <summary>按名册次序取第一个能演的，暂时不能演的那项不挡后面</summary>
        private static bool TryPickNext(Player player, out HimayoBossGiftNarrative gift, out string giftKey) {
            foreach (HimayoGiftEntry entry in HimayoGiftCatalog.All) {
                if (!spawned.Contains(entry.MeiKey)
                    || !scenariosByGiftKey.TryGetValue(entry.MeiKey, out HimayoBossGiftNarrative candidate)) {
                    continue;
                }
                if (candidate.CheckGiftCompleted()) {
                    spawned.Remove(entry.MeiKey);
                    continue;
                }
                if (!candidate.MeetsAdditionalConditions(player)
                    || NarrativeRunner.IsScenarioActiveOrPending(candidate.Key)) {
                    continue;
                }

                gift = candidate;
                giftKey = entry.MeiKey;
                return true;
            }

            gift = null;
            giftKey = null;
            return false;
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

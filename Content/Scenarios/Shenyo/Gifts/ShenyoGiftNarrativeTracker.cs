using CalamityOverhaul.Content.LegendWeapon.KikasaLegend;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaTalismans;
using CalamityOverhaul.Content.LegendWeapon.TrialQuests;
using CalamityOverhaul.Content.Narrative;
using CalamityOverhaul.Content.Narrative.Common;
using CalamityOverhaul.Content.Narrative.Data;
using CalamityOverhaul.Content.Narrative.Data.Modules;
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

        /// <summary>
        /// 默认直接读沉宴试炼线自己的完成位，天然覆盖二选一/多首领合并，通常无需子类重写。<br/>
        /// 这是"能不能演"的就绪位，由 <see cref="ShenyoGiftNarrativeTracker"/> 每 tick 复查，
        /// <b>不可</b>在击杀回调里判定：击杀登记跑在 <c>HitEffect</c>，早于 <c>checkDead</c> 落下首领完成旗标
        /// </summary>
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
        //断档补发的会话限额:满包落地捡不起来时防巡检连发成堆
        private static readonly HashSet<string> repairedGiftIds = new(StringComparer.Ordinal);
        private static bool wasFinaleDowned;

        public static int LastDefeatedBossId(string giftId)
            => lastDefeatedById.TryGetValue(giftId, out int bossId) ? bossId : 0;

        public static void ResetWorldState() {
            spawned.Clear();
            lastDefeatedById.Clear();
            repairedGiftIds.Clear();
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

        /// <summary>
        /// 击杀只做登记，不判就绪：本回调跑在 <c>HitEffect</c>，此刻 <c>checkDead</c> 还没走完，
        /// 首领完成旗标一律是上一场的旧值，在这里读试炼线等于把每份礼物都推迟到"再杀一次"。<br/>
        /// 试炼线的完成位改由 <see cref="TryPickNext"/> 每 tick 复查，三机械/星流双首领这类合并关照旧要凑齐才演
        /// </summary>
        public static void NotifyBossDefeated(int bossId) {
            //终焉之战里首领成串地死，逐个登记会在事件结束后连演一整排；那场戏走 TickFinaleEdge
            if (CWRRef.GetBossRushActive() || !byBossId.TryGetValue(bossId, out List<ShenyoGiftEntry> gifts)) {
                return;
            }

            for (int i = 0; i < gifts.Count; i++) {
                if (gifts[i].TargetBossIds.Length > 1) {
                    lastDefeatedById[gifts[i].Id] = bossId;
                }
                if (scenariosById.ContainsKey(gifts[i].Id)) {
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
            bool edge = downed && !wasFinaleDowned;
            wasFinaleDowned = downed;

            Player player = Main.LocalPlayer;
            if (player?.active != true
                || !scenariosById.TryGetValue(FinaleGiftId, out ShenyoBossGiftNarrative finale)) {
                return;
            }
            ShenyoGiftStoryData giftStory = ShenyoStorySync.GiftStory;
            //旗只落一次且永不复位,边沿只活一帧:目击落旗却在演出前退档的,
            //重进后镜像初始化成 true,礼物就此永失。边沿落持久位,演过(完成位)即自清
            if (edge) {
                giftStory.BossRushGiftPending = true;
            }
            if (!giftStory.BossRushGiftPending) {
                return;
            }
            if (finale.CheckGiftCompleted()) {
                giftStory.BossRushGiftPending = false;
                return;
            }
            //就绪与否留给 TryPickNext 复查，否则这一帧不就绪就永远错过
            spawned.Add(FinaleGiftId);
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
            //低频补发:完成位开演即写,递符与补录符箧都在对白中段——中途退档/断线的那张符就此断档
            if (Main.GameUpdateCount % 620 == 310) {
                RepairLostTalismans(player);
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

        /// <summary>
        /// 唤雨符断档补发:场景完成位已写、符箧未录且身上(含鼠标)无符纸,
        /// 说明发放步被打断丢件——镜像 GiftTalisman 的幂等契约,先补录符箧再补发符纸
        /// </summary>
        private static void RepairLostTalismans(Player player) {
            foreach (ShenyoGiftEntry entry in ShenyoGiftCatalog.All) {
                if (repairedGiftIds.Contains(entry.Id)
                    || !scenariosById.TryGetValue(entry.Id, out ShenyoBossGiftNarrative gift)
                    || !gift.CheckGiftCompleted()
                    || KikasaTalismanOwned.Owns(player, entry.TalismanKey)) {
                    continue;
                }
                int itemType = KikasaTalismanItem.ItemTypeForKey(entry.TalismanKey);
                if (itemType <= 0 || player.HasItem(itemType)) {
                    continue;
                }
                Item mouse = Main.mouseItem;
                if (mouse != null && !mouse.IsAir && mouse.type == itemType) {
                    continue;
                }
                repairedGiftIds.Add(entry.Id);
                KikasaTalismanOwned.Unlock(player, entry.TalismanKey);
                player.QuickSpawnItem(player.GetSource_Misc("CWR_ShenyoGiftRepair"), itemType);
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
                //就绪位在这里复查而非登记时：击杀那一帧旗标还没落，三机械这类合并关也要等最后一只凑齐
                if (!candidate.ShouldSpawn()
                    || !candidate.MeetsAdditionalConditions(player)
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

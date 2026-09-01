using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDrowns;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.Inscriptions;
using CalamityOverhaul.Content.Narrative;
using CalamityOverhaul.Content.Narrative.Common;
using CalamityOverhaul.Content.Narrative.Data;
using CalamityOverhaul.Content.Narrative.Data.Modules;
using InnoVault.Narrative.Core;
using InnoVault.Narrative.Runtime;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

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
        //断档补发的会话限额:满包落地捡不起来时防巡检连发成堆
        private static readonly HashSet<string> repairedGiftKeys = new(StringComparer.Ordinal);
        private static bool wasDownedBossRush;
        private static int lastEvilBossId;

        /// <summary>邪恶首领分支取词：本次击杀优先，未记则按世界的邪恶属性</summary>
        public static int LastDefeatedBossId
            => lastEvilBossId == NPCID.EaterofWorldsHead || lastEvilBossId == NPCID.BrainofCthulhu
                ? lastEvilBossId
                : WorldGen.crimson ? NPCID.BrainofCthulhu : NPCID.EaterofWorldsHead;

        public static void ResetWorldState() {
            spawned.Clear();
            repairedGiftKeys.Clear();
            HimayoGiftNet.ClearDelivered();
            lastEvilBossId = 0;
            wasDownedBossRush = CWRRef.Has && CWRRef.GetDownedBossRush();
            RegisterAll();
        }

        /// <summary>该首领身份是否挂着礼物；死亡入口的服务器端预筛（名册在 OnWorldLoad 两端都注册）</summary>
        internal static bool IsGiftBoss(int bossId) => byBossId.ContainsKey(bossId);

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
            bool edge = downed && !wasDownedBossRush;
            wasDownedBossRush = downed;

            Player player = Main.LocalPlayer;
            if (player?.active != true) {
                return;
            }
            HimayoGiftStoryData giftStory = HimayoStorySync.GetGift(player);
            if (giftStory == null) {
                return;
            }
            //旗只落一次且永不复位,边沿只活一帧:目击落旗却在演出前退档的,
            //重进后 ResetWorldState 会把镜像初始化成 true,礼物就此永失。
            //边沿落持久位,待演状态跨会话认账,演过(完成位)即自清
            if (edge) {
                giftStory.BossRushGiftPending = true;
            }
            if (!giftStory.BossRushGiftPending) {
                return;
            }
            foreach (HimayoGiftEntry entry in HimayoGiftCatalog.All) {
                if (!scenariosByGiftKey.TryGetValue(entry.MeiKey, out HimayoBossGiftNarrative gift)
                    || !gift.IsBossRushGift) {
                    continue;
                }
                if (gift.CheckGiftCompleted()) {
                    giftStory.BossRushGiftPending = false;
                    continue;
                }
                if (gift.ShouldSpawn()) {
                    spawned.Add(entry.MeiKey);
                }
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
            //低频补发:完成位开演即写,拓本却在对白中段递出——中途退档/断线的那份就此断档,
            //而礼物铭没有刀縁回路,等于这只角色永缺一枚铭。补回承诺的拓本(入包即解锁)
            if (Main.GameUpdateCount % 620 == 310) {
                RepairLostRubbings(player);
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

        /// <summary>
        /// 拓本断档补发:场景完成位已写、铭却未持有且身上(含鼠标)无拓本,
        /// 说明发放步被打断丢件——按名册补发一份,拓本入包即幂等解锁
        /// </summary>
        private static void RepairLostRubbings(Player player) {
            foreach (HimayoGiftEntry entry in HimayoGiftCatalog.All) {
                if (repairedGiftKeys.Contains(entry.MeiKey)
                    || !scenariosByGiftKey.TryGetValue(entry.MeiKey, out HimayoBossGiftNarrative gift)
                    || !gift.CheckGiftCompleted()
                    || OniMeiOwned.Owns(player, entry.MeiKey)) {
                    continue;
                }
                int itemType = entry.RubbingItemType;
                if (itemType <= 0 || player.HasItem(itemType)) {
                    continue;
                }
                Item mouse = Main.mouseItem;
                if (mouse != null && !mouse.IsAir && mouse.type == itemType) {
                    continue;
                }
                repairedGiftKeys.Add(entry.MeiKey);
                player.QuickSpawnItem(player.GetSource_Misc("CWR_HimayoGiftRepair"), itemType);
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

    /// <summary>
    /// 礼物击杀的死亡入口：击杀与参战判定只在服务器/单机端可靠——多人客户端本地的
    /// playerInteraction 恒空，且灾厄锁血假死的 HitEffect 会在旁观端白给入队，
    /// 正是"没打过 Boss 也拿铭文、试炼却不认"的分叉源（反馈四·#38）。
    /// 与试炼台账 <see cref="LegendWeapon.TrialQuests.LegendTrialKillNPC"/> 同口径：
    /// 服务器读参战名单逐个单播，客户端只等包
    /// </summary>
    internal sealed class HimayoGiftBossKillNPC : DeathTrackingNPC
    {
        public override bool AppliesToEntity(NPC entity, bool lateInstantiation) => true;

        public override void OnNPCDeath(NPC npc) {
            if (VaultUtils.isClient) {
                return;
            }
            if (CWRRef.GetBossRushActive()) {
                //BossRush 击杀不给常规礼物（专属礼物走 TickBossRushEdge），包也不必发
                return;
            }
            //蠕虫按门槛身份归并（打哪一节都记头），与礼物名册的主体类型直接可比，
            //参战口径与原版战利品一致：打过任意一节即算
            int identity = KikasaBossGate.IdentityTypeOf(npc);
            if (!HimayoGiftNarrativeTracker.IsGiftBoss(identity)) {
                return;
            }
            for (int i = 0; i < Main.maxPlayers; i++) {
                if (!npc.playerInteraction[i] || Main.player[i]?.active != true) {
                    continue;
                }
                HimayoGiftNet.Deliver(i, identity);
            }
        }
    }

    /// <summary>
    /// 礼物击杀的投递信道：服务器把归并后的首领身份单播给参战客户端，
    /// 各端在本机入队叙事（队列与演出全程本地，数据契约不变）
    /// </summary>
    internal sealed class HimayoGiftNet : CWRNetChannel
    {
        //会话内已投递抑制：蠕虫逐节死亡逐节触发，同人同首领只投递一次（镜像试炼的 Record 抑制）
        private static readonly HashSet<long> delivered = [];

        internal static void ClearDelivered() => delivered.Clear();

        internal static void Deliver(int playerIndex, int bossId) {
            if (playerIndex < 0 || playerIndex >= Main.maxPlayers
                || !delivered.Add(((long)playerIndex << 32) | (uint)bossId)) {
                return;
            }
            if (Main.netMode != NetmodeID.Server) {
                //单机：本地直接入队
                HimayoGiftNarrativeTracker.NotifyBossDefeated(bossId);
                return;
            }
            if (!Main.dedServ && playerIndex == Main.myPlayer) {
                //听服房主自己：本地入队，不发包
                HimayoGiftNarrativeTracker.NotifyBossDefeated(bossId);
                return;
            }
            ModPacket packet = CWRNetWork.GetPacket<HimayoGiftNet>();
            packet.Write(bossId);
            packet.Send(toClient: playerIndex);
        }

        public override void Receive(BinaryReader reader, int whoAmI) {
            //先读净载荷再判端，保流对齐
            int bossId = reader.ReadInt32();
            if (Main.netMode != NetmodeID.MultiplayerClient) {
                return;
            }
            HimayoGiftNarrativeTracker.NotifyBossDefeated(bossId);
        }
    }
}

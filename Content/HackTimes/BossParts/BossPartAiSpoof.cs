using CalamityOverhaul.Content.TimeFreezes;
using System.Collections.Generic;
using System.Reflection;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.HackTimes.BossParts
{
    /// <summary>
    /// F1 落地：NPC AI 前后配对的值伪装。<br/>
    /// 两条通道——肢体征收把「部件 AI 读到的玩家位置」换成本体中心，
    /// 协同断链把 Calamity 的 Exo 协同槽临时写成 -1。<br/>
    /// 配对根基：tML 的 <c>NPCLoader.NPCAI</c> 里 <c>PostAI</c> 无条件运行
    /// （PreAI 返回 false 只跳过 AI 本体），InnoVault 的全局 override 提前拦截时
    /// 整个 NPCAI 不跑，两个钩子都不执行——要么成对、要么全无。
    /// 唯一能拆散配对的是 AI 抛异常，由 <see cref="BossPartSpoofGuard"/> 帧末兜底还原
    /// </summary>
    internal static class BossPartAiSpoof
    {
        //伪装记录的新鲜窗口：协议 OnTick 在 PostUpdateEverything 刷新，
        //供下一帧的 AI 消费，隔一帧是常态，超过两帧就当效果已经没了
        private const ulong FreshFrames = 2;

        #region 肢体征收通道

        private struct SeizureRecord
        {
            public int AnchorIndex;
            public ulong Stamp;
        }

        //部件槽位 → 征收记录。权威端专用（伪装只在权威端跑）
        private static readonly Dictionary<int, SeizureRecord> seizedParts = [];

        /// <summary>征收协议每个权威 Tick 刷新一次；停止刷新即自动失效</summary>
        internal static void RefreshSeizure(int partIndex, int anchorIndex) {
            if (partIndex < 0 || partIndex >= Main.maxNPCs
                || anchorIndex < 0 || anchorIndex >= Main.maxNPCs) {
                return;
            }
            seizedParts[partIndex] = new SeizureRecord {
                AnchorIndex = anchorIndex,
                Stamp = Main.GameUpdateCount,
            };
        }

        internal static void ClearSeizure(int partIndex) => seizedParts.Remove(partIndex);

        /// <summary>该部件当前是否处于征收窗口，顺带给出活着的本体</summary>
        internal static bool TryGetSeizureAnchor(NPC part, out NPC anchor) {
            anchor = null;
            if (part == null || !seizedParts.TryGetValue(part.whoAmI,
                out SeizureRecord record)) {
                return false;
            }
            ulong now = Main.GameUpdateCount;
            if (now < record.Stamp || now - record.Stamp > FreshFrames) {
                return false;
            }
            NPC candidate = Main.npc[record.AnchorIndex];
            if (!candidate.active || candidate.life <= 0) {
                return false;
            }
            anchor = candidate;
            return true;
        }

        //本帧被挪走的玩家，PostAI 按原值放回。NPC 串行更新，同一时刻至多一组
        private struct PlayerPosBackup
        {
            public int PlayerIndex;
            public Vector2 Position;
            public Vector2 Velocity;
        }

        private static readonly List<PlayerPosBackup> playerBackups = [];

        private static void SpoofPlayerToAnchor(int playerIndex, Vector2 anchorCenter) {
            if (playerIndex < 0 || playerIndex >= Main.maxPlayers) {
                return;
            }
            //同一玩家只备份一次，第二次伪装会把伪装值当原值存进去
            for (int i = 0; i < playerBackups.Count; i++) {
                if (playerBackups[i].PlayerIndex == playerIndex) {
                    return;
                }
            }
            Player player = Main.player[playerIndex];
            if (player?.active != true) {
                return;
            }
            playerBackups.Add(new PlayerPosBackup {
                PlayerIndex = playerIndex,
                Position = player.position,
                Velocity = player.velocity,
            });
            player.Center = anchorCenter;
            player.velocity = Vector2.Zero;
        }

        private static void RestoreSpoofedPlayers() {
            for (int i = 0; i < playerBackups.Count; i++) {
                PlayerPosBackup backup = playerBackups[i];
                Player player = Main.player[backup.PlayerIndex];
                //无条件按原值放回：AI 对玩家位置的任何写入都不能留下来，
                //把玩家真传送到 Boss 身边是比瞄准出错严重得多的事故
                player.position = backup.Position;
                player.velocity = backup.Velocity;
            }
            playerBackups.Clear();
        }

        #endregion

        #region 协同断链通道

        private static ulong linkCutStamp;
        private static bool linkCutArmed;

        /// <summary>断链协议每个权威 Tick 刷新；停止刷新即窗口自动关闭</summary>
        internal static void RefreshLinkCut() {
            linkCutStamp = Main.GameUpdateCount;
            linkCutArmed = true;
        }

        internal static void ClearLinkCut() => linkCutArmed = false;

        internal static bool LinkCutWindowActive {
            get {
                if (!linkCutArmed) {
                    return false;
                }
                ulong now = Main.GameUpdateCount;
                return now >= linkCutStamp && now - linkCutStamp <= FreshFrames;
            }
        }

        //本次 AI 调用里被清掉的协同槽原值
        private static readonly int[] exoSlotBackup = new int[ExoLinkRef.SlotCount];
        private static bool exoSlotsSpoofed;

        private static void SpoofExoSlots() {
            if (exoSlotsSpoofed || !ExoLinkRef.Ready) {
                return;
            }
            for (int i = 0; i < ExoLinkRef.SlotCount; i++) {
                exoSlotBackup[i] = ExoLinkRef.ReadSlot(i);
                ExoLinkRef.WriteSlot(i, -1);
            }
            exoSlotsSpoofed = true;
        }

        private static void RestoreExoSlots() {
            if (!exoSlotsSpoofed) {
                return;
            }
            for (int i = 0; i < ExoLinkRef.SlotCount; i++) {
                //AI 在窗口内的新注册（AresBody 每帧写回自己的 whoAmI）比备份新鲜，
                //只把仍是 -1 的槽还原，绝不用旧值盖掉刚写入的注册
                if (ExoLinkRef.ReadSlot(i) == -1) {
                    ExoLinkRef.WriteSlot(i, exoSlotBackup[i]);
                }
            }
            exoSlotsSpoofed = false;
        }

        #endregion

        #region 应用与还原

        /// <summary>PreAI 侧：给这只 NPC 的本次 AI 布置伪装</summary>
        internal static void ApplyForNpc(NPC npc) {
            if (Main.netMode == NetmodeID.MultiplayerClient
                || WorldFreezeSystem.IsActive) {
                return;
            }
            //上一次的残留（AI 异常等罕见路径）先清干净再布新的
            if (playerBackups.Count > 0 || exoSlotsSpoofed) {
                RestoreAll();
            }

            if (TryGetSeizureAnchor(npc, out NPC anchor)) {
                //部件自己的 target 与本体的 target 都要罩住：
                //Ares 炮组读的是本体的 target，Ravager 爪读自己的
                SpoofPlayerToAnchor(npc.target, anchor.Center);
                SpoofPlayerToAnchor(anchor.target, anchor.Center);
            }

            if (LinkCutWindowActive && BossPartResolver.IsExoGroupMember(npc)) {
                SpoofExoSlots();
            }
        }

        /// <summary>PostAI 侧：与 <see cref="ApplyForNpc"/> 成对的还原</summary>
        internal static void RestoreAll() {
            RestoreSpoofedPlayers();
            RestoreExoSlots();
        }

        internal static void ResetState() {
            seizedParts.Clear();
            playerBackups.Clear();
            exoSlotsSpoofed = false;
            linkCutArmed = false;
            linkCutStamp = 0;
        }

        //记录随协议 OnRemove 清除；协议被追踪器静默丢弃（目标失效不走 OnRemove）时
        //靠 Stamp 过期失效，这里定期把过期条目从表里摘掉，防止长局字典无界膨胀
        internal static void PruneStaleRecords() {
            if (seizedParts.Count == 0) {
                return;
            }
            ulong now = Main.GameUpdateCount;
            List<int> stale = null;
            foreach (KeyValuePair<int, SeizureRecord> pair in seizedParts) {
                if (now >= pair.Value.Stamp && now - pair.Value.Stamp <= 120) {
                    continue;
                }
                (stale ??= []).Add(pair.Key);
            }
            if (stale == null) {
                return;
            }
            for (int i = 0; i < stale.Count; i++) {
                seizedParts.Remove(stale[i]);
            }
        }

        #endregion
    }

    /// <summary>伪装 pass 的 NPC 钩子。PostAI 在 tML 里无条件运行，与 PreAI 天然成对</summary>
    internal class BossPartSpoofNPC : GlobalNPC
    {
        public override bool PreAI(NPC npc) {
            BossPartAiSpoof.ApplyForNpc(npc);
            return true;
        }

        public override void PostAI(NPC npc) => BossPartAiSpoof.RestoreAll();
    }

    /// <summary>
    /// 帧末兜底：AI 抛异常跳过 PostAI 时，这里把没还的账强制还掉。
    /// PostUpdateNPCs 紧跟 NPC 循环，赶在弹幕与绘制读到脏值之前
    /// </summary>
    internal class BossPartSpoofGuard : ModSystem
    {
        public override void PostUpdateNPCs() {
            BossPartAiSpoof.RestoreAll();
            if (Main.GameUpdateCount % 30 == 0) {
                BossPartAiSpoof.PruneStaleRecords();
            }
        }

        //切世界时把伪装态与协议账本一起清空：ActivationId 属于上一局
        public override void ClearWorld() {
            BossPartAiSpoof.ResetState();
            Protocols.SegmentDelink.ResetLedgers();
            Protocols.LimbSeizure.ResetLedgers();
        }

        public override void Unload() {
            BossPartAiSpoof.ResetState();
            ExoLinkRef.ResetCache();
        }
    }

    /// <summary>
    /// Exo 协同图的反射缓存。零编译期 Calamity 引用，逐成员空守卫：
    /// 任何一个字段取不到，<see cref="Ready"/> 为 false，协同断链整条失活。<br/>
    /// 这四个槽 Calamity 每帧自校验并由各机体 AI 重新注册，
    /// 极端情况下漏还一帧也会在下一帧被上游自愈，不会留下永久坏值
    /// </summary>
    internal static class ExoLinkRef
    {
        internal const int SlotCount = 4;

        private static readonly string[] SlotFieldNames = [
            "draedonExoMechPrime",
            "draedonExoMechTwinGreen",
            "draedonExoMechTwinRed",
            "draedonExoMechWorm",
        ];

        private static readonly FieldInfo[] slotFields = new FieldInfo[SlotCount];
        private static bool initialized;
        private static bool ready;

        internal static bool Ready {
            get {
                EnsureInit();
                return ready;
            }
        }

        private static void EnsureInit() {
            if (initialized) {
                return;
            }
            initialized = true;
            ready = false;
            if (!CWRRef.Has || !ModLoader.TryGetMod("CalamityMod", out Mod calamity)) {
                return;
            }
            System.Type globalNpcType = calamity.Code?
                .GetType("CalamityMod.NPCs.CalamityGlobalNPC");
            if (globalNpcType == null) {
                CWRMod.Instance.Logger.Info(
                    "[BossPart] CalamityGlobalNPC type missing, CommandLinkCut disabled");
                return;
            }
            bool allFound = true;
            for (int i = 0; i < SlotCount; i++) {
                slotFields[i] = globalNpcType.GetField(SlotFieldNames[i],
                    BindingFlags.Public | BindingFlags.Static);
                if (slotFields[i] == null || slotFields[i].FieldType != typeof(int)) {
                    CWRMod.Instance.Logger.Info(
                        $"[BossPart] exo slot field {SlotFieldNames[i]} missing, "
                        + "CommandLinkCut disabled");
                    slotFields[i] = null;
                    allFound = false;
                }
            }
            ready = allFound;
        }

        internal static int ReadSlot(int index) {
            FieldInfo field = slotFields[index];
            return field == null ? -1 : (int)field.GetValue(null);
        }

        internal static void WriteSlot(int index, int value)
            => slotFields[index]?.SetValue(null, value);

        internal static void ResetCache() {
            for (int i = 0; i < SlotCount; i++) {
                slotFields[i] = null;
            }
            initialized = false;
            ready = false;
        }
    }
}

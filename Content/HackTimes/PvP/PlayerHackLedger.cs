using CalamityOverhaul.Content.HackTimes.PvP.UI;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.HackTimes.PvP
{
    /// <summary>
    /// 防守方帐本里的一条已生效协议。<b>条目本身就是 HUD 条目</b>
    /// 框架直接遍历帐本画效果卡，协议无法拥有"不可见的可感知效果"（反幽灵卡顿铁律）。<br/>
    /// per-effect 状态挂 <see cref="ProtocolState"/>，随条目自清，不开协议侧静态字典
    /// </summary>
    internal sealed class PlayerHackEffect
    {
        /// <summary>服务端分配的全局激活号，跨端唯一身份</summary>
        public long ActivationId;
        /// <summary>协议注册序号（QuickHackDef.Instances 索引，仅当前 build 有效）</summary>
        public int SlotIndex;
        /// <summary>协议实例；一定是 <see cref="PlayerHackDef"/></summary>
        public PlayerHackDef Hack;
        /// <summary>施加者玩家索引</summary>
        public int CasterIndex;
        /// <summary>施加者名字。槽位复用双检 + 攻击方掉线后红线端点仍能标名</summary>
        public string CasterName = string.Empty;
        /// <summary>已流逝帧数。防守方本机推进，这里是全网真值时钟</summary>
        public int Elapsed;
        /// <summary>总时长（帧），服务端授予时随 DefenderApply 下发</summary>
        public int Duration;
        public bool Active = true;
        /// <summary>协议自定 per-effect 状态（在 OnDefenderApply 里 new 出来挂上）</summary>
        public object ProtocolState;

        /// <summary>剩余比例 0..1，HUD 时长条直读</summary>
        public float RemainingRatio => Duration <= 0 ? 0f
            : MathHelper.Clamp(1f - Elapsed / (float)Duration, 0f, 1f);

        public int RemainingFrames => Math.Max(Duration - Elapsed, 0);
    }

    /// <summary>来袭上传（DefenderNotice 数据），被骇横幅的数据源</summary>
    internal sealed class PlayerHackNotice
    {
        public int AttackerIndex;
        public string AttackerName = string.Empty;
        public uint SessionId;
        public uint RequestId;
        public int SlotIndex;
        /// <summary>0=上传中 1=已取消 2=已失败 3=已落地（终止态驱动横幅退场演出）</summary>
        public byte State;
        public int Elapsed;
        public int UploadFrames;
        /// <summary>自清 TTL：每次收到 Notice 重置为 45f，丢终止包也能超时自清</summary>
        public int Ttl;
        /// <summary>首达警示音已播（7.5 律：记 beat 防重放）</summary>
        public bool PlayedCue;
        /// <summary>两次 Notice 之间的本机线性外推基准帧</summary>
        public ulong LastUpdateFrame;

        public bool Terminal => State != 0;

        /// <summary>带 15f 间隔补间的显示进度 0..1</summary>
        public float DisplayProgress {
            get {
                if (UploadFrames <= 0) return 0f;
                float extrapolated = Elapsed;
                if (!Terminal) {
                    extrapolated += Math.Min(
                        (float)(Main.GameUpdateCount - LastUpdateFrame), 20f);
                }
                return MathHelper.Clamp(extrapolated / UploadFrames, 0f, 1f);
            }
        }
    }

    /// <summary>链路回溯点亮的攻击方穿墙标记（只在施术者本机存在）</summary>
    internal sealed class PlayerHackMarker
    {
        public int AttackerIndex;
        public string AttackerName = string.Empty;
        public int FramesLeft;
    }

    /// <summary>
    /// 防守方本机真值帐本（ModPlayer）。<br/>
    /// <b>归属</b>：只有拥有者客户端的实例持有效果真值（防守方客户端结算是接受的信任边界）；
    /// 服务端的真值是 <see cref="PlayerHackAuthority"/> 授予账，两账靠 300f 周期对账报文
    /// 与 60f 影子广播互相自愈。<br/>
    /// <b>推进</b>：由 <c>PlayerHackSystem.PostUpdateEverything</c> 驱动（死人不跑 PostUpdate，
    /// 不能挂 ModPlayer.PostUpdate，tml-netcode-pitfalls §5.1 三次翻车的原坑），
    /// <see cref="UpdateDead"/> 双保险负责死亡清账。<br/>
    /// <b>HUD 契约</b>：ActiveEffects 每一条都会被画成效果卡，IncomingUploads 每一条都会
    /// 进被骇横幅，可感知效果必有 HUD 条目由这里的结构保证，不靠协议作者自觉
    /// </summary>
    internal sealed class PlayerHackLedger : ModPlayer
    {
        private const int NoticeTtlFrames = 45;
        private const int MaxTombstones = 256;
        /// <summary>对账报文周期（帧）</summary>
        internal const int ReportIntervalFrames = 300;

        private readonly List<PlayerHackEffect> activeEffects = [];
        private readonly List<PlayerHackNotice> notices = [];
        private readonly List<PlayerHackMarker> tracebackMarkers = [];
        //已移除激活号墓碑：迟到/重复的 DefenderApply 不得复活效果（照抄 replicatedTombstones）
        private readonly HashSet<long> tombstones = [];
        private readonly Queue<long> tombstoneOrder = [];
        private int reportTimer;
        private bool wasDead;

        #region 只读 API（HUD 与第二波协议从这里读，别绕过去改内部表）

        /// <summary>在册效果，落地序（[0] 最早）。HUD 效果条与强制卸载的"最早一条"都按这个序</summary>
        internal IReadOnlyList<PlayerHackEffect> ActiveEffects => activeEffects;

        /// <summary>来袭上传（含刚终止未过 TTL 的），被骇横幅数据源</summary>
        internal IReadOnlyList<PlayerHackNotice> IncomingUploads => notices;

        /// <summary>链路回溯点亮的攻击方标记（只在施术者本机非空）</summary>
        internal IReadOnlyList<PlayerHackMarker> TracebackMarkers => tracebackMarkers;

        /// <summary>是否有任何在册敌方效果（强制卸载的 CanApplyTo 用）</summary>
        internal bool HasHostileEffects => activeEffects.Count > 0;

        /// <summary>是否有仍在上传中的来袭（链路回溯的 CanApplyTo 用）</summary>
        internal bool HasActiveIncomingUpload {
            get {
                for (int i = 0; i < notices.Count; i++) {
                    if (!notices[i].Terminal) return true;
                }
                return false;
            }
        }

        /// <summary>按激活号找条目，无则 null</summary>
        internal PlayerHackEffect FindEffect(long activationId) {
            for (int i = 0; i < activeEffects.Count; i++) {
                if (activeEffects[i].ActivationId == activationId) return activeEffects[i];
            }
            return null;
        }

        /// <summary>指定协议是否在册（协议互斥/HUD 查询用）</summary>
        internal bool HasEffect<T>() where T : PlayerHackDef {
            for (int i = 0; i < activeEffects.Count; i++) {
                if (activeEffects[i].Hack is T) return true;
            }
            return false;
        }

        #endregion

        #region 反制冷却（本机镜像供面板灰显；服务端实例上的这两个值才是校验真值）

        /// <summary>链路回溯自身冷却（帧）。两端各自倒数，服务端实例为真值</summary>
        internal int TracebackCooldown;
        /// <summary>强制卸载自身冷却（帧）</summary>
        internal int UninstallCooldown;

        /// <summary>复活保护剩余帧。死亡→复活翻转时置 300f，在册期间不可被选中</summary>
        internal int SpawnProtectFrames { get; private set; }

        #endregion

        #region 生命周期

        public override void Initialize() => ClearAll();

        public override void PlayerDisconnect() => ClearAll();

        //死亡清账的双保险：PlayerHackSystem 的全员遍历是主驱动，
        //这里兜住"系统遍历与死亡发生在同帧"的缝
        public override void UpdateDead() {
            if (Player.whoAmI == Main.myPlayer && activeEffects.Count > 0) {
                ClearEffectsLocal(PlayerHackRemoveReason.DefenderLost);
            }
        }

        private void ClearAll() {
            activeEffects.Clear();
            notices.Clear();
            tracebackMarkers.Clear();
            tombstones.Clear();
            tombstoneOrder.Clear();
            TracebackCooldown = 0;
            UninstallCooldown = 0;
            SpawnProtectFrames = 0;
            reportTimer = 0;
            wasDead = false;
        }

        #endregion

        #region 写入口（只允许 PlayerHackNet 管线与两条反制协议调）

        /// <summary>
        /// 本机施加（DefenderApply 落地）。幂等：在册同激活号直接返回 true（重发回执即可，
        /// 不重复施加）；踩到墓碑返回 false（已移除的效果不复活）。
        /// 调用方（PlayerHackNet）负责先做本机终审与载荷读取
        /// </summary>
        internal bool TryApplyLocal(PlayerHackEffect effect) {
            if (effect == null || effect.ActivationId <= 0 || effect.Hack == null) {
                return false;
            }
            if (tombstones.Contains(effect.ActivationId)) return false;
            if (FindEffect(effect.ActivationId) != null) return true;

            if (!effect.Hack.OnDefenderApply(Player, effect)) return false;
            activeEffects.Add(effect);
            return true;
        }

        /// <summary>本机移除（到期/广播移除/强制卸载），执行协议清理并立墓碑</summary>
        internal bool RemoveLocal(long activationId, PlayerHackRemoveReason reason) {
            AddTombstone(activationId);
            PlayerHackEffect effect = FindEffect(activationId);
            if (effect == null) return false;
            effect.Active = false;
            activeEffects.Remove(effect);
            effect.Hack.OnDefenderRemove(Player, effect, reason);
            PlayerHackHudFeed.NotifyEffectRemoved(effect, reason);
            return true;
        }

        /// <summary>全清（死亡/清账），逐条走协议清理</summary>
        internal void ClearEffectsLocal(PlayerHackRemoveReason reason) {
            for (int i = activeEffects.Count - 1; i >= 0; i--) {
                RemoveLocal(activeEffects[i].ActivationId, reason);
            }
        }

        /// <summary>收 DefenderNotice：按 (attacker, session, request) 建/更新横幅条目</summary>
        internal void UpsertNotice(int attackerIndex, uint sessionId, uint requestId,
            int slotIndex, byte state, int elapsed, int uploadFrames) {
            PlayerHackNotice entry = null;
            for (int i = 0; i < notices.Count; i++) {
                PlayerHackNotice candidate = notices[i];
                if (candidate.AttackerIndex == attackerIndex
                    && candidate.SessionId == sessionId
                    && candidate.RequestId == requestId) {
                    entry = candidate;
                    break;
                }
            }
            if (entry == null) {
                entry = new PlayerHackNotice {
                    AttackerIndex = attackerIndex,
                    AttackerName = ResolveName(attackerIndex),
                    SessionId = sessionId,
                    RequestId = requestId,
                };
                notices.Add(entry);
            }
            entry.SlotIndex = slotIndex;
            entry.State = state;
            entry.Elapsed = elapsed;
            entry.UploadFrames = Math.Max(uploadFrames, 1);
            entry.Ttl = NoticeTtlFrames;
            entry.LastUpdateFrame = Main.GameUpdateCount;
            PlayerHackHudFeed.NotifyNotice(entry);
        }

        /// <summary>链路回溯落地：点亮攻击方标记（施术者本机）</summary>
        internal void AddTracebackMarker(int attackerIndex, int frames) {
            for (int i = 0; i < tracebackMarkers.Count; i++) {
                if (tracebackMarkers[i].AttackerIndex == attackerIndex) {
                    tracebackMarkers[i].FramesLeft
                        = Math.Max(tracebackMarkers[i].FramesLeft, frames);
                    return;
                }
            }
            tracebackMarkers.Add(new PlayerHackMarker {
                AttackerIndex = attackerIndex,
                AttackerName = ResolveName(attackerIndex),
                FramesLeft = frames,
            });
        }

        #endregion

        #region 推进（PlayerHackSystem 驱动）

        /// <summary>
        /// 每帧推进，仅拥有者客户端调（效果真值时钟在这里走）。
        /// 死亡帧由 UpdateDead 清账，这里看到 dead 直接跳过
        /// </summary>
        internal void TickLocalTruth() {
            if (Player.dead || Player.ghost) {
                if (activeEffects.Count > 0) {
                    ClearEffectsLocal(PlayerHackRemoveReason.DefenderLost);
                }
                //死亡期间来袭上传由服务端取消，本机横幅只等 TTL 自清
                TickNotices();
                return;
            }

            //效果真值时钟：推进、Tick、到期
            for (int i = activeEffects.Count - 1; i >= 0; i--) {
                PlayerHackEffect effect = activeEffects[i];
                effect.Elapsed++;
                bool keep = effect.Hack.OnDefenderTick(Player, effect);
                if (!keep || effect.Elapsed >= effect.Duration) {
                    RemoveLocal(effect.ActivationId, PlayerHackRemoveReason.Expired);
                }
            }

            TickNotices();

            //回溯标记倒数
            for (int i = tracebackMarkers.Count - 1; i >= 0; i--) {
                if (--tracebackMarkers[i].FramesLeft <= 0) {
                    tracebackMarkers.RemoveAt(i);
                }
            }

            //周期对账：把在册激活号上报服务端（审计痕，不是执法）
            if (++reportTimer >= ReportIntervalFrames) {
                reportTimer = 0;
                PlayerHackNet.SendLedgerReport(this);
            }
        }

        /// <summary>
        /// 每帧轻推进，所有端所有玩家实例都调（冷却/复活保护是玩家层的表，
        /// 服务端实例上的值就是校验真值）
        /// </summary>
        internal void TickShared() {
            if (TracebackCooldown > 0) TracebackCooldown--;
            if (UninstallCooldown > 0) UninstallCooldown--;

            //死亡→复活翻转即挂 300f 复活保护
            if (Player.dead) {
                wasDead = true;
                SpawnProtectFrames = 0;
            }
            else if (wasDead) {
                wasDead = false;
                SpawnProtectFrames = HackPvPRules.SpawnProtectFrames;
            }
            else if (SpawnProtectFrames > 0) {
                SpawnProtectFrames--;
            }
        }

        private void TickNotices() {
            for (int i = notices.Count - 1; i >= 0; i--) {
                if (--notices[i].Ttl <= 0) {
                    notices.RemoveAt(i);
                }
            }
        }

        #endregion

        private void AddTombstone(long activationId) {
            if (activationId <= 0 || !tombstones.Add(activationId)) return;
            tombstoneOrder.Enqueue(activationId);
            while (tombstones.Count > MaxTombstones
                && tombstoneOrder.TryDequeue(out long expired)) {
                tombstones.Remove(expired);
            }
        }

        private static string ResolveName(int playerIndex) {
            if (playerIndex < 0 || playerIndex >= Main.maxPlayers) return string.Empty;
            return Main.player[playerIndex]?.name ?? string.Empty;
        }

        /// <summary>把在册激活号写进对账报文</summary>
        internal void CollectActivationIds(List<long> result) {
            result.Clear();
            for (int i = 0; i < activeEffects.Count; i++) {
                result.Add(activeEffects[i].ActivationId);
            }
        }
    }
}

using CalamityOverhaul.Content.HackTimes.PvP.UI;
using CalamityOverhaul.Content.HackTimes.Scannables;
using CalamityOverhaul.Content.RAMSystems;
using InnoVault.Cinematics;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.HackTimes.PvP
{
    /// <summary>服务端授予账里的一条已授予效果（防守方帐本的影子）</summary>
    internal sealed class PlayerHackGrant
    {
        public long ActivationId;
        public int CasterIndex;
        /// <summary>施加者名字，槽位复用双检（tml-netcode-pitfalls §4.2）</summary>
        public string CasterName = string.Empty;
        public int DefenderIndex;
        /// <summary>防守方名字，看门狗与对账时槽位名字对不上视同断线清账</summary>
        public string DefenderName = string.Empty;
        public int SlotIndex;
        public PlayerHackDef Hack;
        public int Duration;
        /// <summary>攻击方实付 RAM，回执超时/拒绝时全额退</summary>
        public float PaidRamCost;
        /// <summary>已收到 Applied 回执（转正）</summary>
        public bool Confirmed;
        /// <summary>未转正时的回执窗口倒数（120f）</summary>
        public int ReceiptTimer = PlayerHackAuthority.ReceiptWindowFrames;
        /// <summary>服务端影子时钟（转正后推进）</summary>
        public int ShadowElapsed;
        /// <summary>影子到期后的宽限倒数，走完仍无对账确认就强制广播移除</summary>
        public int WatchdogGrace = PlayerHackAuthority.WatchdogGraceFrames;
        /// <summary>连续几次对账报文里没有这条（3 次 → Logger.Warn 审计痕）</summary>
        public int MissedReports;
        /// <summary>协议自定的服务端载荷（额度/计数），随条目自清</summary>
        public object AuthorityState;
    }

    /// <summary>
    /// PvP 骇入的服务端账房：授予账（per-defender）、对冷却、复活保护查询、
    /// 反制协议的服务端裁决。全部是<b>服务端世界级 static</b>（单人=本机权威），
    /// 世界卸载与防守方断线时清账。客户端不要读这里——观众数据走 <see cref="PlayerHackMirror"/>
    /// </summary>
    internal static class PlayerHackAuthority
    {
        /// <summary>DefenderApply 的回执窗口（帧），超时撤销授予并全额退 RAM</summary>
        internal const int ReceiptWindowFrames = 120;
        /// <summary>影子到期后的看门狗宽限（帧）</summary>
        internal const int WatchdogGraceFrames = 90;
        /// <summary>影子广播周期（帧）：攻击方剩余时长显示、迟到者补面、丢包自愈</summary>
        internal const int ShadowBroadcastInterval = 60;
        /// <summary>上传中断：攻击方单帧受伤 ≥ 该比例 × statLifeMax → 全部 PvP 上传作废退半</summary>
        internal const float HurtInterruptRatio = 0.08f;
        /// <summary>上传中断：拉出距离的宽限帧数</summary>
        internal const int OutOfRangeGraceFrames = 45;
        /// <summary>取消退款比例（打断/拉距/hostile 翻转）</summary>
        internal const float CancelRefundRatio = 0.5f;
        /// <summary>链路回溯：每个被作废的攻击方吃的 RAM 烧蚀</summary>
        internal const int TracebackScorch = 2;
        /// <summary>链路回溯：攻击方位置对施术者穿墙标记时长</summary>
        internal const int TracebackMarkFrames = 900;
        /// <summary>链路回溯自身冷却</summary>
        internal const int TracebackCooldownFrames = 900;
        /// <summary>强制卸载自身冷却</summary>
        internal const int UninstallCooldownFrames = 1800;

        //授予账：defenderIndex → 落地序授予列表
        private static readonly Dictionary<int, List<PlayerHackGrant>> grants = [];
        //对冷却：(attacker, defender) → 到期帧
        private static readonly Dictionary<(byte, byte), ulong> pairCooldowns = [];
        //上传的拉距宽限计数，键 = 上传实例（取消/完成时随上传一起消失）
        private static readonly Dictionary<AuthorityHackUpload, int> outOfRangeFrames = [];
        //攻击方受击打断：上一帧生命镜像 + 本帧打断旗
        private static readonly int[] lastLife = new int[Main.maxPlayers];
        private static readonly bool[] hurtInterrupt = new bool[Main.maxPlayers];
        //影子广播计时（per defender）
        private static readonly Dictionary<int, int> broadcastTimers = [];
        //ScanProbe 限频：requester → 上次受理帧
        private static readonly Dictionary<int, ulong> probeStamps = [];

        #region 查询（HackPvPRules 的子句数据源；数据缺席的端返回"放行"）

        /// <summary>该防守方的在册效果数（含待回执——授予即占坑，防回执窗口内超发）</summary>
        internal static int CountEffectsOn(int defenderIndex) {
            if (Main.netMode == NetmodeID.MultiplayerClient) {
                return PlayerHackMirror.CountEffectsOn(defenderIndex);
            }
            return grants.TryGetValue(defenderIndex, out var list) ? list.Count : 0;
        }

        /// <summary>同 (攻击方, 防守方) 对的在册效果数</summary>
        internal static int CountEffectsOnPair(int attackerIndex, int defenderIndex) {
            if (Main.netMode == NetmodeID.MultiplayerClient) {
                return PlayerHackMirror.CountEffectsOnPair(attackerIndex, defenderIndex);
            }
            if (!grants.TryGetValue(defenderIndex, out var list)) return 0;
            int count = 0;
            for (int i = 0; i < list.Count; i++) {
                if (list[i].CasterIndex == attackerIndex) count++;
            }
            return count;
        }

        /// <summary>对冷却查询。服务端读真值账；客户端读攻击方本机镜像（只知道自己那份）</summary>
        internal static bool IsPairOnCooldown(int attackerIndex, int defenderIndex) {
            if (Main.netMode == NetmodeID.MultiplayerClient) {
                return PlayerHackMirror.IsOwnPairOnCooldown(attackerIndex, defenderIndex);
            }
            return pairCooldowns.TryGetValue(((byte)attackerIndex, (byte)defenderIndex),
                out ulong until) && Main.GameUpdateCount < until;
        }

        /// <summary>复活保护查询：数据在各端的 <see cref="PlayerHackLedger"/> 实例上</summary>
        internal static bool IsSpawnProtected(int defenderIndex) {
            if (defenderIndex < 0 || defenderIndex >= Main.maxPlayers) return false;
            Player player = Main.player[defenderIndex];
            if (player?.active != true) return false;
            return player.TryGetModPlayer(out PlayerHackLedger ledger)
                && ledger.SpawnProtectFrames > 0;
        }

        /// <summary>该玩家（作为防守方）身上是否有授予（强制卸载的服务端 CanApplyTo）</summary>
        internal static bool HasGrantsOn(int defenderIndex)
            => grants.TryGetValue(defenderIndex, out var list) && list.Count > 0;

        /// <summary>是否有任何玩家的上传正瞄着该玩家（链路回溯的服务端 CanApplyTo）</summary>
        internal static bool HasUploadsAimedAt(int defenderIndex) {
            for (int i = 0; i < Main.maxPlayers; i++) {
                Player attacker = Main.player[i];
                if (attacker?.active != true) continue;
                var state = attacker.GetModPlayer<HackTimeAuthorityPlayer>();
                for (int j = 0; j < state.Uploads.Count; j++) {
                    if (IsUploadAimedAt(state.Uploads[j], defenderIndex)) return true;
                }
            }
            return false;
        }

        private static bool IsUploadAimedAt(AuthorityHackUpload upload, int defenderIndex)
            => upload.TargetIdentity.Kind == HackTargetKind.Player
                && upload.TargetIdentity.PlayerIndex == defenderIndex;

        #endregion

        #region 授予与撤销

        /// <summary>
        /// 上传完成 → 立授予账 + 发 DefenderApply。当前实现必定成功返回授予条目
        /// （失败语义由回执窗口兜底：防守方拒绝/失联走 Revoke 全退）；
        /// 调用方的判空是防御性写法，不是既有失败路径
        /// </summary>
        internal static PlayerHackGrant Grant(Player caster, Player defender,
            PlayerHackDef hack, float paidRamCost) {
            long activationId = HackTimeNetSync.AllocateActivationId();
            var grant = new PlayerHackGrant {
                ActivationId = activationId,
                CasterIndex = caster.whoAmI,
                CasterName = caster.name,
                DefenderIndex = defender.whoAmI,
                DefenderName = defender.name,
                SlotIndex = hack.SlotIndex,
                Hack = hack,
                Duration = Math.Max(hack.GetDuration(), 0),
                PaidRamCost = paidRamCost,
            };
            if (!grants.TryGetValue(defender.whoAmI, out var list)) {
                list = [];
                grants[defender.whoAmI] = list;
            }
            list.Add(grant);
            //权威侧结算钩（服务端拥有的资源在这里落账：RAM 烧蚀、榨取额度）
            hack.OnAuthorityGranted(caster, defender, grant);
            PlayerHackNet.SendDefenderApply(grant, caster, defender);
            return grant;
        }

        /// <summary>撤销一条授予：清协议权威账、广播移除、可选全额退 RAM</summary>
        internal static void Revoke(PlayerHackGrant grant, PlayerHackRemoveReason reason,
            bool refundCaster) {
            if (!grants.TryGetValue(grant.DefenderIndex, out var list)
                || !list.Remove(grant)) {
                return;
            }
            grant.Hack?.OnAuthorityRevoked(grant, reason);
            if (refundCaster && grant.PaidRamCost > 0f) {
                Player caster = ResolveActive(grant.CasterIndex);
                if (caster != null) RamSystem.Restore(caster, grant.PaidRamCost, out _);
            }
            PlayerHackNet.BroadcastEffectRemove(grant.ActivationId, reason);
        }

        /// <summary>收到 Applied 回执：授予转正 + 开对冷却 + 广播全员状态</summary>
        internal static void ConfirmGrant(PlayerHackGrant grant) {
            grant.Confirmed = true;
            pairCooldowns[((byte)grant.CasterIndex, (byte)grant.DefenderIndex)]
                = Main.GameUpdateCount + (ulong)HackPvPRules.PairCooldownFrames;
            PlayerHackNet.BroadcastEffectState(grant.DefenderIndex);
        }

        internal static PlayerHackGrant FindGrant(long activationId) {
            foreach (var pair in grants) {
                List<PlayerHackGrant> list = pair.Value;
                for (int i = 0; i < list.Count; i++) {
                    if (list[i].ActivationId == activationId) return list[i];
                }
            }
            return null;
        }

        /// <summary>把某防守方的已转正授予收进列表（广播打包用）</summary>
        internal static void CollectConfirmed(int defenderIndex,
            List<PlayerHackGrant> result) {
            result.Clear();
            if (!grants.TryGetValue(defenderIndex, out var list)) return;
            for (int i = 0; i < list.Count; i++) {
                if (list[i].Confirmed) result.Add(list[i]);
            }
        }

        /// <summary>防守方掉线/死亡/换世界：撤销其全部待回执与在册条目并广播移除</summary>
        internal static void ClearDefender(int defenderIndex, PlayerHackRemoveReason reason) {
            if (!grants.TryGetValue(defenderIndex, out var list) || list.Count == 0) {
                return;
            }
            for (int i = list.Count - 1; i >= 0; i--) {
                PlayerHackGrant grant = list[i];
                //待回执的授予退全款（效果从未生效）；已转正的不退（合法施加过）
                Revoke(grant, reason, refundCaster: !grant.Confirmed);
            }
        }

        #endregion

        #region 反制协议的服务端裁决（LinkTraceback / ForceUninstall 的 OnApply 调进来）

        /// <summary>
        /// 链路回溯：作废所有瞄着施术者的上传（攻击方 RAM 不退——白丢是攻击方的风险成本）、
        /// 每个被作废的攻击方吃 2 RAM 烧蚀（RAM 归服务端，直写）、
        /// 攻击方位置对施术者穿墙标记 900f。返回被作废的攻击方数
        /// </summary>
        internal static int ExecuteTraceback(Player caster) {
            List<int> traced = [];
            for (int i = 0; i < Main.maxPlayers; i++) {
                Player attacker = Main.player[i];
                if (attacker?.active != true) continue;
                var state = attacker.GetModPlayer<HackTimeAuthorityPlayer>();
                bool any = false;
                for (int j = state.Uploads.Count - 1; j >= 0; j--) {
                    AuthorityHackUpload upload = state.Uploads[j];
                    if (!IsUploadAimedAt(upload, caster.whoAmI)) continue;
                    //回溯作废：不退 RAM；行移除走 accepted:false 的 QueueState
                    CancelUpload(attacker, state, j, refundRatio: 0f,
                        noticeState: 1, logReason: "traceback");
                    any = true;
                }
                if (!any) continue;
                ScorchRam(attacker, TracebackScorch);
                traced.Add(i);
            }

            //冷却真值记在服务端的帐本实例上；施术者本机在 OnReplicatedApply 里镜像
            if (caster.TryGetModPlayer(out PlayerHackLedger casterLedger)) {
                casterLedger.TracebackCooldown = TracebackCooldownFrames;
            }
            if (Main.netMode == NetmodeID.Server) {
                PlayerHackNet.SendTracebackResult(caster.whoAmI, traced);
                for (int i = 0; i < traced.Count; i++) {
                    PlayerHackNet.SendAlert(traced[i], PlayerHackAlert.Traced, 0);
                }
            }
            else {
                //单人不存在别的玩家，走不到这里；防御性保留本机标记落点
                for (int i = 0; i < traced.Count; i++) {
                    casterLedger?.AddTracebackMarker(traced[i], TracebackMarkFrames);
                }
            }
            return traced.Count;
        }

        /// <summary>
        /// 强制卸载：拔掉施术者（作为防守方）身上最早落地的一条授予并广播移除。
        /// 授予账与防守方帐本同为落地序，按账头拔即可对齐"最早一条"
        /// </summary>
        internal static bool ExecuteUninstall(Player caster) {
            if (!grants.TryGetValue(caster.whoAmI, out var list) || list.Count == 0) {
                return false;
            }
            PlayerHackGrant earliest = null;
            for (int i = 0; i < list.Count; i++) {
                if (list[i].Confirmed) {
                    earliest = list[i];
                    break;
                }
            }
            earliest ??= list[0];
            Revoke(earliest, PlayerHackRemoveReason.Uninstalled, refundCaster: false);

            if (caster.TryGetModPlayer(out PlayerHackLedger ledger)) {
                ledger.UninstallCooldown = UninstallCooldownFrames;
            }
            return true;
        }

        /// <summary>RAM 烧蚀：烧到多少算多少（TryConsume 是全有或全无，逐级降额）</summary>
        private static void ScorchRam(Player target, int amount) {
            for (int burn = HackPvPRules.ClampRamScorch(amount); burn > 0; burn--) {
                if (RamSystem.TryConsume(target, burn, out _)) return;
            }
        }

        #endregion

        #region 上传期逐帧（HackTimeNetSync.UpdatePlayerUploads 的 Player 分流调进来）

        /// <summary>
        /// Player 目标上传的逐帧推进（权威端）。接管共享循环的 elapsed 推进、
        /// 进度广播（攻击方 QueueState + 防守方 DefenderNotice）、每 tick 准入重验、
        /// 完成后的授予分流（不进 HackEffectTracker）。<br/>
        /// 返回 true = 该上传应由<b>调用方</b>从队列移除（完成或取消，本方法不动表）
        /// </summary>
        internal static bool TickUpload(Player caster, HackTimeAuthorityPlayer state,
            AuthorityHackUpload upload) {
            int slot = upload.TargetIdentity.PlayerIndex;
            Player defender = ResolveActive(slot);
            QuickHackDef hack = QuickHackDef.GetByIndex(upload.SlotIndex);

            if (defender == null || defender.dead || hack is not PlayerHackDef playerHack) {
                //目标已下线/已死：取消退半（上传已开始消耗了对方的反应窗口）
                SettleCancel(caster, upload, CancelRefundRatio,
                    noticeState: 1, "target lost");
                return true;
            }

            //攻击方受击打断（服务端按 msg 16 生命镜像判定，单帧掉血 ≥ 8% lifeMax）
            if (hurtInterrupt[caster.whoAmI]) {
                SettleCancel(caster, upload, CancelRefundRatio,
                    noticeState: 1, "attacker hurt interrupt");
                return true;
            }

            //每 tick 准入重验：hostile 中途关闭、换队、复活保护等即刻取消；
            //距离越界单独走 45f 宽限（瞬移/钩爪拉扯不该一帧掐断）
            if (!HackPvPRules.CanTarget(caster, defender,
                out HackRequestResultCode denied)) {
                bool rangeOnly = denied == HackRequestResultCode.OutOfRange;
                if (!rangeOnly) {
                    SettleCancel(caster, upload, CancelRefundRatio,
                        noticeState: 1, $"revalidation failed: {denied}");
                    return true;
                }
                int strikes = outOfRangeFrames.TryGetValue(upload, out int s) ? s + 1 : 1;
                if (strikes >= OutOfRangeGraceFrames) {
                    SettleCancel(caster, upload, CancelRefundRatio,
                        noticeState: 1, "out of range");
                    return true;
                }
                outOfRangeFrames[upload] = strikes;
            }
            else {
                outOfRangeFrames.Remove(upload);
            }

            upload.Elapsed = Math.Min(upload.Elapsed + 1, upload.UploadFrames);

            //进度双播：攻击方 15f 一份 QueueState（原样），防守方同拍一份 Notice
            //（首帧也发一份——被骇横幅在接受当帧就要亮）
            if (Main.netMode == NetmodeID.Server
                && (upload.Elapsed == 1 || upload.Elapsed % 15 == 0)) {
                HackTimeNetSync.SendQueueState(caster.whoAmI, upload.SessionId,
                    upload.RequestId, upload.SlotIndex, upload.State, upload.Elapsed,
                    upload.UploadFrames, 0, upload.TargetIdentity, accepted: true,
                    caster.whoAmI);
                PlayerHackNet.SendDefenderNotice(defender.whoAmI, caster.whoAmI,
                    upload.SessionId, upload.RequestId, upload.SlotIndex,
                    state: 0, upload.Elapsed, upload.UploadFrames);
            }
            if (upload.Elapsed < upload.UploadFrames) return false;

            //上传完成 → 授予（不调 ApplyAuthorityEffect——那是权威端施加，玩家目标没有这一步）
            outOfRangeFrames.Remove(upload);
            PlayerHackGrant grant = Grant(caster, defender, playerHack,
                upload.PaidRamCost);
            upload.State = HackQueueState.Completed;
            upload.ActivationId = grant?.ActivationId ?? 0;
            if (Main.netMode == NetmodeID.Server) {
                HackTimeNetSync.SendQueueState(caster.whoAmI, upload.SessionId,
                    upload.RequestId, upload.SlotIndex, upload.State,
                    upload.UploadFrames, upload.UploadFrames, upload.ActivationId,
                    upload.TargetIdentity, accepted: grant != null, caster.whoAmI);
                //落地终止 Notice：横幅白闪翻红、交棒给效果条目
                PlayerHackNet.SendDefenderNotice(defender.whoAmI, caster.whoAmI,
                    upload.SessionId, upload.RequestId, upload.SlotIndex,
                    state: 3, upload.UploadFrames, upload.UploadFrames);
            }
            return true;
        }

        /// <summary>取消一条 PvP 上传并从队列移除（链路回溯的批量作废走这里）</summary>
        private static void CancelUpload(Player caster, HackTimeAuthorityPlayer state,
            int uploadIndex, float refundRatio, byte noticeState, string logReason) {
            AuthorityHackUpload upload = state.Uploads[uploadIndex];
            state.Uploads.RemoveAt(uploadIndex);
            SettleCancel(caster, upload, refundRatio, noticeState, logReason);
        }

        /// <summary>
        /// 取消结算（不动队列表，移除责任归调用方）：退款、通知攻防两端、
        /// 记日志（拒绝必须点名子句）
        /// </summary>
        private static void SettleCancel(Player caster, AuthorityHackUpload upload,
            float refundRatio, byte noticeState, string logReason) {
            outOfRangeFrames.Remove(upload);

            if (refundRatio > 0f && upload.PaidRamCost > 0f) {
                RamSystem.Restore(caster, upload.PaidRamCost * refundRatio, out _);
            }
            if (Main.netMode == NetmodeID.Server) {
                CWRMod.Instance.Logger.Info(
                    $"[HackPvP] canceled {caster.name}'s upload "
                    + $"slot={upload.SlotIndex} → player#{upload.TargetIdentity.PlayerIndex}"
                    + $": {logReason} (refund {refundRatio:P0})");
                HackTimeNetSync.SendQueueState(caster.whoAmI, upload.SessionId,
                    upload.RequestId, upload.SlotIndex, upload.State, upload.Elapsed,
                    upload.UploadFrames, 0, upload.TargetIdentity, accepted: false,
                    caster.whoAmI);
                int defenderIndex = upload.TargetIdentity.PlayerIndex;
                if (defenderIndex >= 0 && defenderIndex < Main.maxPlayers) {
                    PlayerHackNet.SendDefenderNotice(defenderIndex, caster.whoAmI,
                        upload.SessionId, upload.RequestId, upload.SlotIndex,
                        noticeState, upload.Elapsed, upload.UploadFrames);
                }
            }
        }

        #endregion

        #region 服务端脉冲（PlayerHackSystem 驱动，每帧一次）

        internal static void UpdateAuthority() {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            UpdateHurtInterrupt();

            //授予账逐防守方推进：回执窗口、影子时钟、看门狗、断线清账
            //（快照复制集合，Revoke 会改字典）
            List<int> defenders = [.. grants.Keys];
            for (int d = 0; d < defenders.Count; d++) {
                int defenderIndex = defenders[d];
                if (!grants.TryGetValue(defenderIndex, out var list)
                    || list.Count == 0) {
                    grants.Remove(defenderIndex);
                    continue;
                }

                Player defender = ResolveActive(defenderIndex);
                //防守方离场/死亡/名字对不上（槽位换人）→ 清账
                if (defender == null || defender.dead
                    || (list.Count > 0 && list[0].DefenderName != defender.name)) {
                    ClearDefender(defenderIndex, PlayerHackRemoveReason.DefenderLost);
                    broadcastTimers.Remove(defenderIndex);
                    continue;
                }

                for (int i = list.Count - 1; i >= 0; i--) {
                    PlayerHackGrant grant = list[i];
                    if (!grant.Confirmed) {
                        //回执窗口：超时 = 目标失联，撤销授予 + 全额退 + 告知攻击方
                        if (--grant.ReceiptTimer > 0) continue;
                        Revoke(grant, PlayerHackRemoveReason.Watchdog, refundCaster: true);
                        if (Main.netMode == NetmodeID.Server) {
                            CWRMod.Instance.Logger.Info(
                                $"[HackPvP] receipt timeout: {grant.CasterName}'s "
                                + $"slot={grant.SlotIndex} on {grant.DefenderName}, refunded");
                            PlayerHackNet.SendAlert(grant.CasterIndex,
                                PlayerHackAlert.TargetLost, 0);
                        }
                        continue;
                    }
                    //影子时钟：到期 + 宽限仍未见防守方对账确认 → 强制移除
                    //（防"防守方崩溃后效果在攻击方屏上永生"）
                    grant.ShadowElapsed++;
                    if (grant.ShadowElapsed >= grant.Duration
                        && --grant.WatchdogGrace <= 0) {
                        Revoke(grant, PlayerHackRemoveReason.Expired, refundCaster: false);
                    }
                }

                //60f 影子广播（攻击方剩余时长显示、迟到者补面、丢包自愈）
                int timer = broadcastTimers.TryGetValue(defenderIndex, out int t) ? t : 0;
                if (++timer >= ShadowBroadcastInterval) {
                    timer = 0;
                    PlayerHackNet.BroadcastEffectState(defenderIndex);
                }
                broadcastTimers[defenderIndex] = timer;
            }
        }

        //攻击方受击打断的生命镜像：msg 16 自报生命在服务端可见，
        //单帧掉血 ≥ 8% lifeMax 的那一帧点旗，TickUpload 消费
        private static void UpdateHurtInterrupt() {
            for (int i = 0; i < Main.maxPlayers; i++) {
                Player player = Main.player[i];
                if (player?.active != true || player.dead) {
                    lastLife[i] = 0;
                    hurtInterrupt[i] = false;
                    continue;
                }
                int drop = lastLife[i] - player.statLife;
                hurtInterrupt[i] = lastLife[i] > 0
                    && drop >= (int)(player.statLifeMax2 * HurtInterruptRatio);
                lastLife[i] = player.statLife;
            }
        }

        /// <summary>对账报文比对：授予了但对面从不在册 → 审计日志（不是执法）</summary>
        internal static void ReconcileReport(int defenderIndex,
            HashSet<long> reportedIds) {
            if (!grants.TryGetValue(defenderIndex, out var list)) return;
            for (int i = list.Count - 1; i >= 0; i--) {
                PlayerHackGrant grant = list[i];
                if (!grant.Confirmed) continue;
                if (reportedIds.Contains(grant.ActivationId)) {
                    grant.MissedReports = 0;
                    continue;
                }
                if (++grant.MissedReports < 3) continue;
                CWRMod.Instance.Logger.Warn(
                    $"[HackPvP] audit: {grant.DefenderName} never ledgered "
                    + $"activation {grant.ActivationId} ({grant.Hack?.Name}, "
                    + $"caster {grant.CasterName}) — 3 consecutive reports missing");
                grant.MissedReports = 0;
            }
        }

        /// <summary>ScanProbe 服务端限频：1 次 / 60f / 攻击方</summary>
        internal static bool AllowProbe(int requesterIndex) {
            ulong now = Main.GameUpdateCount;
            if (probeStamps.TryGetValue(requesterIndex, out ulong last)
                && now - last < 60) {
                return false;
            }
            probeStamps[requesterIndex] = now;
            return true;
        }

        #endregion

        /// <summary>世界卸载清账（服务端 static 只对当前世界有效）</summary>
        internal static void Reset() {
            grants.Clear();
            pairCooldowns.Clear();
            outOfRangeFrames.Clear();
            broadcastTimers.Clear();
            probeStamps.Clear();
            Array.Clear(lastLife);
            Array.Clear(hurtInterrupt);
        }

        private static Player ResolveActive(int index) {
            if (index < 0 || index >= Main.maxPlayers) return null;
            Player player = Main.player[index];
            return player?.active == true ? player : null;
        }
    }

    /// <summary>攻击方侧的反制警报种类（PvPAlert 载荷）</summary>
    internal enum PlayerHackAlert : byte
    {
        /// <summary>链路被回溯：上传全灭 + 吃烧蚀 + 被标记</summary>
        Traced,
        /// <summary>目标失联：回执超时，RAM 已全退</summary>
        TargetLost,
        /// <summary>防守方本机拒绝（携带拒绝码），RAM 已全退</summary>
        Rejected,
    }

    /// <summary>客户端探针快照（扫描面板的服务端行）</summary>
    internal readonly record struct PlayerProbeData(
        int Defense,
        byte RamBand,
        byte ImplantCount,
        bool FirewallDetected,
        ushort ProtocolCount,
        ulong Frame)
    {
        internal bool IsFresh => Main.GameUpdateCount - Frame < 300;
    }

    /// <summary>
    /// 客户端镜像：PlayerEffectState 喂进来的全员在册效果视图。<br/>
    /// 攻击方 HUD（植入物面板）、旁观者表现（故障光环）、客户端预检的叠加计数
    /// 全从这里读，<b>不读防守方本机任何值</b>（7.1 教训：效果强度不读观众值）
    /// </summary>
    internal static class PlayerHackMirror
    {
        internal sealed class MirrorEffect
        {
            public long ActivationId;
            public int DefenderIndex;
            public int CasterIndex;
            public int SlotIndex;
            public int Elapsed;
            public int Duration;
            /// <summary>本机首见帧（红线 90f 全强度窗口从这里起算）</summary>
            public ulong FirstSeenFrame;
            /// <summary>移除原因（收到 Remove 后短暂保留供退场演出）</summary>
            public PlayerHackRemoveReason? RemovedReason;
            /// <summary>退场演出剩余帧</summary>
            public int RemoveFxFrames;

            public float RemainingRatio => Duration <= 0 ? 0f
                : MathHelper.Clamp(1f - Elapsed / (float)Duration, 0f, 1f);
        }

        private static readonly List<MirrorEffect> effects = [];
        private static readonly HashSet<long> tombstones = [];
        private static readonly Queue<long> tombstoneOrder = [];
        //攻击方本机的对冷却镜像：defenderIndex → 到期帧（只知道自己那份）
        private static readonly Dictionary<int, ulong> ownPairCooldowns = [];
        //探针缓存与限频
        private static readonly Dictionary<int, PlayerProbeData> probeCache = [];
        private static readonly Dictionary<int, ulong> probeRequestStamps = [];
        private const int MaxTombstones = 256;

        internal static IReadOnlyList<MirrorEffect> All => effects;

        #region 查询

        internal static int CountEffectsOn(int defenderIndex) {
            int count = 0;
            for (int i = 0; i < effects.Count; i++) {
                if (effects[i].DefenderIndex == defenderIndex
                    && effects[i].RemovedReason == null) count++;
            }
            return count;
        }

        internal static int CountEffectsOnPair(int attackerIndex, int defenderIndex) {
            int count = 0;
            for (int i = 0; i < effects.Count; i++) {
                MirrorEffect fx = effects[i];
                if (fx.DefenderIndex == defenderIndex && fx.CasterIndex == attackerIndex
                    && fx.RemovedReason == null) count++;
            }
            return count;
        }

        internal static bool IsOwnPairOnCooldown(int attackerIndex, int defenderIndex) {
            if (attackerIndex != Main.myPlayer) return false;
            return ownPairCooldowns.TryGetValue(defenderIndex, out ulong until)
                && Main.GameUpdateCount < until;
        }

        /// <summary>本机玩家在别人身上的在册效果（攻击方植入物面板数据源）</summary>
        internal static void CollectOwnImplants(List<MirrorEffect> result) {
            result.Clear();
            for (int i = 0; i < effects.Count; i++) {
                if (effects[i].CasterIndex == Main.myPlayer) result.Add(effects[i]);
            }
        }

        /// <summary>某防守方身上的在册效果（旁观者光环密度 = 条数）</summary>
        internal static void CollectOnDefender(int defenderIndex,
            List<MirrorEffect> result) {
            result.Clear();
            for (int i = 0; i < effects.Count; i++) {
                if (effects[i].DefenderIndex == defenderIndex
                    && effects[i].RemovedReason == null) {
                    result.Add(effects[i]);
                }
            }
        }

        internal static PlayerProbeData? GetProbe(int defenderIndex)
            => probeCache.TryGetValue(defenderIndex, out PlayerProbeData data)
                ? data : null;

        #endregion

        #region 写入口（PlayerHackNet 的包处理调）

        /// <summary>整面替换某防守方的镜像集（全量快照语义，丢包自愈）</summary>
        internal static void ApplyStateSnapshot(int defenderIndex,
            List<(long id, int caster, int slot, int elapsed, int duration)> records) {
            //先记住哪些是新落地的（攻击方对冷却镜像要用）
            for (int r = 0; r < records.Count; r++) {
                var record = records[r];
                if (tombstones.Contains(record.id)) continue;
                MirrorEffect existing = Find(record.id);
                if (existing != null) {
                    //影子 elapsed 只前进不回拨（7.5 律：回拨快照会重放演出）
                    existing.Elapsed = Math.Max(existing.Elapsed, record.elapsed);
                    existing.Duration = record.duration;
                    continue;
                }
                effects.Add(new MirrorEffect {
                    ActivationId = record.id,
                    DefenderIndex = defenderIndex,
                    CasterIndex = record.caster,
                    SlotIndex = record.slot,
                    Elapsed = record.elapsed,
                    Duration = record.duration,
                    FirstSeenFrame = Main.GameUpdateCount,
                });
                PlayerHackHudFeed.NotifyMirrorLanded(record.id, defenderIndex,
                    record.caster, record.slot);
                //自己的效果落地 → 开本机对冷却镜像（预检变灰用，服务端才是真值）
                if (record.caster == Main.myPlayer) {
                    ownPairCooldowns[defenderIndex] = Main.GameUpdateCount
                        + (ulong)HackPvPRules.PairCooldownFrames;
                }
            }
            //快照里没有的在册条目 = 已被移除但 Remove 包丢了 → 走无演出移除
            for (int i = effects.Count - 1; i >= 0; i--) {
                MirrorEffect fx = effects[i];
                if (fx.DefenderIndex != defenderIndex || fx.RemovedReason != null) {
                    continue;
                }
                bool present = false;
                for (int r = 0; r < records.Count; r++) {
                    if (records[r].id == fx.ActivationId) {
                        present = true;
                        break;
                    }
                }
                if (!present) MarkRemoved(fx, PlayerHackRemoveReason.Expired);
            }
        }

        internal static void ApplyRemove(long activationId,
            PlayerHackRemoveReason reason) {
            AddTombstone(activationId);
            MirrorEffect fx = Find(activationId);
            if (fx != null && fx.RemovedReason == null) MarkRemoved(fx, reason);
        }

        internal static void StoreProbe(int defenderIndex, PlayerProbeData data)
            => probeCache[defenderIndex] = data;

        /// <summary>客户端限频的探针请求（选中玩家目标时面板拉取）</summary>
        internal static void RequestProbe(int defenderIndex) {
            if (Main.netMode != NetmodeID.MultiplayerClient) return;
            ulong now = Main.GameUpdateCount;
            if (probeRequestStamps.TryGetValue(defenderIndex, out ulong last)
                && now - last < 60) {
                return;
            }
            probeRequestStamps[defenderIndex] = now;
            PlayerHackNet.SendScanProbe(defenderIndex);
        }

        #endregion

        /// <summary>每帧推进：影子 elapsed 本机补间、到期自清、退场演出计时、表现钩分发</summary>
        internal static void Tick() {
            for (int i = effects.Count - 1; i >= 0; i--) {
                MirrorEffect fx = effects[i];
                if (fx.RemovedReason != null) {
                    if (--fx.RemoveFxFrames <= 0) effects.RemoveAt(i);
                    continue;
                }
                fx.Elapsed++;
                Player defender = fx.DefenderIndex >= 0
                    && fx.DefenderIndex < Main.maxPlayers
                    ? Main.player[fx.DefenderIndex] : null;
                if (defender?.active != true
                    || (fx.Duration > 0 && fx.Elapsed > fx.Duration + 150)) {
                    //150f = 60f 影子广播 + 90f 看门狗的封顶；错误状态存活不超过它
                    MarkRemoved(fx, PlayerHackRemoveReason.Expired);
                    continue;
                }
                if (QuickHackDef.GetByIndex(fx.SlotIndex) is PlayerHackDef hack
                    && defender != null) {
                    hack.OnSpectatorTick(defender, fx.CasterIndex, fx.Elapsed,
                        fx.Duration);
                }
            }
        }

        private static void MarkRemoved(MirrorEffect fx, PlayerHackRemoveReason reason) {
            fx.RemovedReason = reason;
            fx.RemoveFxFrames = 40;
            AddTombstone(fx.ActivationId);
            PlayerHackHudFeed.NotifyMirrorRemoved(fx, reason);
        }

        private static MirrorEffect Find(long activationId) {
            for (int i = 0; i < effects.Count; i++) {
                if (effects[i].ActivationId == activationId) return effects[i];
            }
            return null;
        }

        private static void AddTombstone(long activationId) {
            if (activationId <= 0 || !tombstones.Add(activationId)) return;
            tombstoneOrder.Enqueue(activationId);
            while (tombstones.Count > MaxTombstones
                && tombstoneOrder.TryDequeue(out long expired)) {
                tombstones.Remove(expired);
            }
        }

        internal static void Reset() {
            effects.Clear();
            tombstones.Clear();
            tombstoneOrder.Clear();
            ownPairCooldowns.Clear();
            probeCache.Clear();
            probeRequestStamps.Clear();
        }
    }

    /// <summary>
    /// PvP 骇入的收发集中地。<see cref="Handle"/> 由 <c>HackTimeNetSync.HandleApplyPacket</c>
    /// 的分发接线调进来（共用文件那侧只有一行）。<br/>
    /// 读包纪律：<b>先把负载吃干净再守卫</b>——CWRNetWork 全家共用一个 reader，
    /// 提前 return 会把字节留在流里错位后续分支（tml-netcode-pitfalls §1.1）。
    /// 变长载荷一律带 1 字节长度前缀进子流
    /// </summary>
    internal static class PlayerHackNet
    {
        private const int MaxPayloadBytes = 255;

        private static ModPacket NewPacket(HackNetOperation operation) {
            ModPacket packet = CWRMod.Instance.GetPacket();
            packet.Write((byte)CWRMessageType.HackProtocolApply);
            packet.Write((byte)operation);
            return packet;
        }

        /// <summary>PvP 批操作的统一入口，非本批操作返回 false 交还上游</summary>
        internal static bool Handle(HackNetOperation operation, BinaryReader reader,
            int whoAmI) {
            switch (operation) {
                case HackNetOperation.ScanProbe:
                    HandleScanProbe(reader, whoAmI);
                    return true;
                case HackNetOperation.ScanProbeReply:
                    HandleScanProbeReply(reader);
                    return true;
                case HackNetOperation.DefenderNotice:
                    HandleDefenderNotice(reader);
                    return true;
                case HackNetOperation.DefenderApply:
                    HandleDefenderApply(reader);
                    return true;
                case HackNetOperation.DefenderReceipt:
                    HandleDefenderReceipt(reader, whoAmI);
                    return true;
                case HackNetOperation.PlayerEffectState:
                    HandleEffectState(reader);
                    return true;
                case HackNetOperation.PlayerEffectRemove:
                    HandleEffectRemove(reader);
                    return true;
                case HackNetOperation.DefenderLedgerReport:
                    HandleLedgerReport(reader, whoAmI);
                    return true;
                case HackNetOperation.TracebackResult:
                    HandleTracebackResult(reader);
                    return true;
                case HackNetOperation.PvPAlert:
                    HandleAlert(reader);
                    return true;
                default:
                    return false;
            }
        }

        #region ScanProbe（扫描静默：防守方不知道被扫）

        internal static void SendScanProbe(int defenderIndex) {
            if (Main.netMode != NetmodeID.MultiplayerClient) return;
            ModPacket packet = NewPacket(HackNetOperation.ScanProbe);
            packet.Write((byte)defenderIndex);
            packet.Send();
        }

        private static void HandleScanProbe(BinaryReader reader, int whoAmI) {
            int defenderIndex = reader.ReadByte();
            if (Main.netMode != NetmodeID.Server
                || whoAmI < 0 || whoAmI >= Main.maxPlayers
                || defenderIndex >= Main.maxPlayers
                || !PlayerHackAuthority.AllowProbe(whoAmI)
                || !HackPvPRules.ServerEnabled) {
                return;
            }
            Player defender = Main.player[defenderIndex];
            if (defender?.active != true) return;

            //RAM 段位：权威只回发本人是总线契约，这里由服务端转述并刻意降精度
            //（侦察给的是态势不是仪表读数）
            RAMPlayer ram = defender.GetModPlayer<RAMPlayer>();
            float ratio = ram.MaxRam > 0 ? ram.Ratio : 0f;
            byte band = ratio <= 0.02f ? (byte)0
                : ratio < 0.3f ? (byte)1
                : ratio < 0.6f ? (byte)2
                : ratio < 0.9f ? (byte)3 : (byte)4;

            int implants = 0;
            var cyber = defender.GetModPlayer<Cyberwares.CyberwarePlayer>();
            Item[] equipped = cyber?.EquippedCyberwares;
            if (equipped != null) {
                for (int i = 0; i < equipped.Length; i++) {
                    if (equipped[i]?.IsAir == false) implants++;
                }
            }

            int protocols = defender.GetModPlayer<HackTimePlayer>()
                .OwnedProtocols.Count;

            ModPacket packet = NewPacket(HackNetOperation.ScanProbeReply);
            packet.Write((byte)defenderIndex);
            packet.Write(defender.statDefense);
            packet.Write(band);
            packet.Write((byte)Math.Clamp(implants, 0, byte.MaxValue));
            //防火墙义体是第三波内容，当前恒未检出；字节位先占住
            packet.Write(false);
            packet.Write((ushort)Math.Clamp(protocols, 0, ushort.MaxValue));
            packet.Send(whoAmI);
        }

        private static void HandleScanProbeReply(BinaryReader reader) {
            int defenderIndex = reader.ReadByte();
            int defense = reader.ReadInt32();
            byte band = reader.ReadByte();
            byte implants = reader.ReadByte();
            bool firewall = reader.ReadBoolean();
            ushort protocols = reader.ReadUInt16();
            if (Main.netMode != NetmodeID.MultiplayerClient
                || defenderIndex >= Main.maxPlayers) {
                return;
            }
            PlayerHackMirror.StoreProbe(defenderIndex, new PlayerProbeData(
                defense, Math.Min(band, (byte)4), implants, firewall, protocols,
                Main.GameUpdateCount));
        }

        #endregion

        #region DefenderNotice（被骇横幅数据源；纯表现流，丢一发下一发自愈）

        internal static void SendDefenderNotice(int defenderIndex, int attackerIndex,
            uint sessionId, uint requestId, int slotIndex, byte state, int elapsed,
            int uploadFrames) {
            if (Main.netMode != NetmodeID.Server || defenderIndex < 0
                || defenderIndex >= Main.maxPlayers) {
                return;
            }
            ModPacket packet = NewPacket(HackNetOperation.DefenderNotice);
            packet.Write((byte)attackerIndex);
            packet.Write(sessionId);
            packet.Write(requestId);
            packet.Write((ushort)slotIndex);
            packet.Write(state);
            packet.Write(elapsed);
            packet.Write(uploadFrames);
            packet.Send(defenderIndex);
        }

        private static void HandleDefenderNotice(BinaryReader reader) {
            int attackerIndex = reader.ReadByte();
            uint sessionId = reader.ReadUInt32();
            uint requestId = reader.ReadUInt32();
            int slotIndex = reader.ReadUInt16();
            byte state = reader.ReadByte();
            int elapsed = reader.ReadInt32();
            int uploadFrames = reader.ReadInt32();
            if (Main.netMode != NetmodeID.MultiplayerClient
                || attackerIndex >= Main.maxPlayers || sessionId == 0
                || requestId == 0 || elapsed < 0 || uploadFrames <= 0
                || uploadFrames > 60 * 60 || elapsed > uploadFrames
                || Main.LocalPlayer?.active != true) {
                return;
            }
            Main.LocalPlayer.GetModPlayer<PlayerHackLedger>().UpsertNotice(
                attackerIndex, sessionId, requestId, slotIndex, state, elapsed,
                uploadFrames);
        }

        #endregion

        #region DefenderApply / DefenderReceipt（授予 → 本机施加 → 回执闭环）

        internal static void SendDefenderApply(PlayerHackGrant grant, Player caster,
            Player defender) {
            if (Main.netMode != NetmodeID.Server) {
                //单人没有第二个玩家，走不到授予；这里只可能是防御性调用
                return;
            }
            //协议施加载荷进长度前缀的子缓冲
            byte[] payload = BuildPayload(w
                => grant.Hack.WriteApplyPayload(w, caster, defender));
            ModPacket packet = NewPacket(HackNetOperation.DefenderApply);
            packet.Write(grant.ActivationId);
            packet.Write((byte)grant.CasterIndex);
            packet.Write((ushort)grant.SlotIndex);
            packet.Write(grant.Duration);
            packet.Write(1f);
            packet.Write((byte)payload.Length);
            packet.Write(payload);
            packet.Send(defender.whoAmI);
        }

        private static void HandleDefenderApply(BinaryReader reader) {
            long activationId = reader.ReadInt64();
            int casterIndex = reader.ReadByte();
            int slotIndex = reader.ReadUInt16();
            int duration = reader.ReadInt32();
            float effectMult = reader.ReadSingle();
            byte[] payload = reader.ReadBytes(reader.ReadByte());
            if (Main.netMode != NetmodeID.MultiplayerClient || activationId <= 0
                || casterIndex >= Main.maxPlayers || duration < 0
                || duration > HackEffectTracker.MaxEffectDuration
                || !float.IsFinite(effectMult)
                || QuickHackDef.GetByIndex(slotIndex) is not PlayerHackDef hack
                || Main.LocalPlayer?.active != true) {
                return;
            }

            Player defender = Main.LocalPlayer;
            var ledger = defender.GetModPlayer<PlayerHackLedger>();

            //本机终审：死亡/幽灵/正在演出/本机叠加上限竞态。
            //演出保护服务端不知道，这正是终审存在的理由之一
            HackRequestResultCode deny = HackRequestResultCode.Success;
            if (defender.dead || defender.ghost) {
                deny = HackRequestResultCode.InvalidTarget;
            }
            else if (CutsceneDirector.IsPlaying) {
                deny = HackRequestResultCode.Unavailable;
            }
            else if (ledger.ActiveEffects.Count >= HackPvPRules.MaxEffectsPerDefender
                && ledger.FindEffect(activationId) == null) {
                deny = HackRequestResultCode.StackLimit;
            }

            if (deny != HackRequestResultCode.Success) {
                SendDefenderReceipt(activationId, (byte)deny, null);
                return;
            }

            var effect = new PlayerHackEffect {
                ActivationId = activationId,
                SlotIndex = slotIndex,
                Hack = hack,
                CasterIndex = casterIndex,
                CasterName = casterIndex >= 0
                    ? Main.player[casterIndex]?.name ?? string.Empty : string.Empty,
                Duration = duration,
            };
            if (payload.Length > 0) {
                using var sub = new BinaryReader(new MemoryStream(payload));
                hack.ReadApplyPayload(sub, effect);
            }

            //幂等：在册即回执 Applied，不重复施加（对齐 ApplyReplicatedEffect 的写法）；
            //重复 Apply 只补回执，不重放 HUD 提示音（7.5 律）
            bool alreadyApplied = ledger.FindEffect(activationId) != null;
            if (!ledger.TryApplyLocal(effect)) {
                SendDefenderReceipt(activationId,
                    (byte)HackRequestResultCode.Unavailable, null);
                return;
            }
            byte[] receiptPayload = BuildPayload(w
                => hack.WriteReceiptPayload(w, ledger.FindEffect(activationId) ?? effect));
            SendDefenderReceipt(activationId, 0, receiptPayload);
            if (!alreadyApplied) {
                PlayerHackHudFeed.NotifyEffectApplied(effect);
            }
        }

        private static void SendDefenderReceipt(long activationId, byte result,
            byte[] payload) {
            if (Main.netMode != NetmodeID.MultiplayerClient) return;
            payload ??= [];
            ModPacket packet = NewPacket(HackNetOperation.DefenderReceipt);
            packet.Write(activationId);
            packet.Write(result);
            packet.Write((byte)payload.Length);
            packet.Write(payload);
            packet.Send();
        }

        private static void HandleDefenderReceipt(BinaryReader reader, int whoAmI) {
            long activationId = reader.ReadInt64();
            byte result = reader.ReadByte();
            byte[] payload = reader.ReadBytes(reader.ReadByte());
            if (Main.netMode != NetmodeID.Server || activationId <= 0) return;

            PlayerHackGrant grant = PlayerHackAuthority.FindGrant(activationId);
            if (grant == null) {
                //迟到回执：授予已超时撤销（RAM 已退）。若对面确实施加了，
                //补一发移除把它的帐本对齐
                if (result == 0) {
                    BroadcastEffectRemove(activationId, PlayerHackRemoveReason.Watchdog);
                }
                return;
            }
            //回执必须来自防守方本人
            if (whoAmI != grant.DefenderIndex) return;
            if (grant.Confirmed) return;

            if (result == 0) {
                if (payload.Length > 0) {
                    Player caster = grant.CasterIndex >= 0
                        && grant.CasterIndex < Main.maxPlayers
                        ? Main.player[grant.CasterIndex] : null;
                    Player defender = Main.player[grant.DefenderIndex];
                    using var sub = new BinaryReader(new MemoryStream(payload));
                    grant.Hack.HandleReceiptPayload(sub, caster, defender, grant);
                }
                PlayerHackAuthority.ConfirmGrant(grant);
                return;
            }
            //防守方本机拒绝：撤销 + 全额退 + 失败反馈 + 点名日志
            CWRMod.Instance.Logger.Info(
                $"[HackPvP] defender {grant.DefenderName} rejected activation "
                + $"{activationId} ({grant.Hack?.Name}): code {result}");
            PlayerHackAuthority.Revoke(grant, PlayerHackRemoveReason.DefenderLost,
                refundCaster: true);
            SendAlert(grant.CasterIndex, PlayerHackAlert.Rejected, result);
        }

        private static byte[] BuildPayload(Action<BinaryWriter> write) {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            write(writer);
            writer.Flush();
            if (stream.Length > MaxPayloadBytes) {
                //载荷设计红线：单条协议载荷超 255 字节说明设计走形，砍到空载荷并记日志
                CWRMod.Instance.Logger.Warn(
                    $"[HackPvP] apply/receipt payload overflow ({stream.Length}B), dropped");
                return [];
            }
            return stream.ToArray();
        }

        #endregion

        #region PlayerEffectState / PlayerEffectRemove（观众表现数据源）

        /// <summary>把某防守方的已转正授予打包广播全员（回执转正与 60f 影子各来一份）</summary>
        internal static void BroadcastEffectState(int defenderIndex) {
            if (Main.netMode != NetmodeID.Server) return;
            List<PlayerHackGrant> confirmed = [];
            PlayerHackAuthority.CollectConfirmed(defenderIndex, confirmed);
            ModPacket packet = NewPacket(HackNetOperation.PlayerEffectState);
            packet.Write((byte)defenderIndex);
            packet.Write((byte)Math.Min(confirmed.Count, byte.MaxValue));
            for (int i = 0; i < confirmed.Count && i < byte.MaxValue; i++) {
                PlayerHackGrant grant = confirmed[i];
                packet.Write(grant.ActivationId);
                packet.Write((byte)grant.CasterIndex);
                packet.Write((ushort)grant.SlotIndex);
                packet.Write(grant.ShadowElapsed);
                packet.Write(grant.Duration);
            }
            packet.Send();
        }

        private static void HandleEffectState(BinaryReader reader) {
            int defenderIndex = reader.ReadByte();
            int count = reader.ReadByte();
            List<(long, int, int, int, int)> records = new(count);
            for (int i = 0; i < count; i++) {
                long id = reader.ReadInt64();
                int caster = reader.ReadByte();
                int slot = reader.ReadUInt16();
                int elapsed = reader.ReadInt32();
                int duration = reader.ReadInt32();
                records.Add((id, caster, slot, elapsed, duration));
            }
            if (Main.netMode != NetmodeID.MultiplayerClient
                || defenderIndex >= Main.maxPlayers) {
                return;
            }
            PlayerHackMirror.ApplyStateSnapshot(defenderIndex, records);
        }

        internal static void BroadcastEffectRemove(long activationId,
            PlayerHackRemoveReason reason) {
            if (Main.netMode != NetmodeID.Server || activationId <= 0) return;
            ModPacket packet = NewPacket(HackNetOperation.PlayerEffectRemove);
            packet.Write(activationId);
            packet.Write((byte)reason);
            packet.Send();
        }

        private static void HandleEffectRemove(BinaryReader reader) {
            long activationId = reader.ReadInt64();
            var reason = (PlayerHackRemoveReason)reader.ReadByte();
            if (Main.netMode != NetmodeID.MultiplayerClient || activationId <= 0) {
                return;
            }
            PlayerHackMirror.ApplyRemove(activationId, reason);
            //移除指向自己的效果 → 帐本照移除原因走对应退场（碎裂 vs 淡出）
            Main.LocalPlayer?.GetModPlayer<PlayerHackLedger>()
                ?.RemoveLocal(activationId, reason);
        }

        #endregion

        #region 对账 / 回溯 / 警报

        internal static void SendLedgerReport(PlayerHackLedger ledger) {
            if (Main.netMode != NetmodeID.MultiplayerClient
                || ledger.Player.whoAmI != Main.myPlayer) {
                return;
            }
            List<long> ids = [];
            ledger.CollectActivationIds(ids);
            ModPacket packet = NewPacket(HackNetOperation.DefenderLedgerReport);
            packet.Write((byte)Math.Min(ids.Count, byte.MaxValue));
            for (int i = 0; i < ids.Count && i < byte.MaxValue; i++) {
                packet.Write(ids[i]);
            }
            packet.Send();
        }

        private static void HandleLedgerReport(BinaryReader reader, int whoAmI) {
            int count = reader.ReadByte();
            HashSet<long> ids = new(count);
            for (int i = 0; i < count; i++) {
                ids.Add(reader.ReadInt64());
            }
            if (Main.netMode != NetmodeID.Server || whoAmI < 0
                || whoAmI >= Main.maxPlayers) {
                return;
            }
            PlayerHackAuthority.ReconcileReport(whoAmI, ids);
        }

        internal static void SendTracebackResult(int casterIndex, List<int> traced) {
            if (Main.netMode != NetmodeID.Server || casterIndex < 0
                || casterIndex >= Main.maxPlayers) {
                return;
            }
            ModPacket packet = NewPacket(HackNetOperation.TracebackResult);
            packet.Write((byte)Math.Min(traced.Count, byte.MaxValue));
            for (int i = 0; i < traced.Count && i < byte.MaxValue; i++) {
                packet.Write((byte)traced[i]);
            }
            packet.Send(casterIndex);
        }

        private static void HandleTracebackResult(BinaryReader reader) {
            int count = reader.ReadByte();
            List<int> traced = new(count);
            for (int i = 0; i < count; i++) {
                traced.Add(reader.ReadByte());
            }
            if (Main.netMode != NetmodeID.MultiplayerClient
                || Main.LocalPlayer?.active != true) {
                return;
            }
            var ledger = Main.LocalPlayer.GetModPlayer<PlayerHackLedger>();
            for (int i = 0; i < traced.Count; i++) {
                if (traced[i] < Main.maxPlayers) {
                    ledger.AddTracebackMarker(traced[i],
                        PlayerHackAuthority.TracebackMarkFrames);
                }
            }
            PlayerHackHudFeed.NotifyTracebackFired(traced.Count);
        }

        internal static void SendAlert(int attackerIndex, PlayerHackAlert kind,
            byte detail) {
            if (Main.netMode != NetmodeID.Server || attackerIndex < 0
                || attackerIndex >= Main.maxPlayers) {
                return;
            }
            ModPacket packet = NewPacket(HackNetOperation.PvPAlert);
            packet.Write((byte)kind);
            packet.Write(detail);
            packet.Send(attackerIndex);
        }

        private static void HandleAlert(BinaryReader reader) {
            var kind = (PlayerHackAlert)reader.ReadByte();
            byte detail = reader.ReadByte();
            if (Main.netMode != NetmodeID.MultiplayerClient) return;
            PlayerHackHudFeed.NotifyAlert(kind, detail);
        }

        #endregion

        internal static void Reset() {
            PlayerHackAuthority.Reset();
            PlayerHackMirror.Reset();
        }
    }

    /// <summary>
    /// PvP 骇入的每帧驱动。刻意独立于 <c>HackTime.PostUpdateEverything</c>——
    /// 不给共用文件加行；ModSystem 钩不吃世界冻结（§5.2），但 HackTime 的世界冻结
    /// 只在单人开，而单人没有 PvP 目标，无需 TimeGear 闸
    /// </summary>
    internal sealed class PlayerHackSystem : ModSystem
    {
        public override void PostSetupContent() {
            //玩家协议的晶粒纹注册进芯片图标管线（HUD 效果卡直取；缺纹样走 FallbackDie）
            for (int i = 0; i < QuickHackDef.Count; i++) {
                if (QuickHackDef.GetByIndex(i) is PlayerHackDef hack
                    && !string.IsNullOrEmpty(hack.GlyphDiePath)) {
                    Chips.HackChipGlyph.Register(hack.GetType().Name,
                        hack.GlyphDiePath);
                }
            }
        }

        public override void PostUpdateEverything() {
            if (Main.gameMenu) return;

            //权威脉冲（服务端/单人）：授予账、回执窗口、影子广播、看门狗、受击打断镜像
            PlayerHackAuthority.UpdateAuthority();

            //冷却/复活保护是玩家层的表，所有端所有玩家实例都推进
            //（服务端实例上的值就是校验真值）
            for (int i = 0; i < Main.maxPlayers; i++) {
                Player player = Main.player[i];
                if (player?.active != true) continue;
                player.GetModPlayer<PlayerHackLedger>().TickShared();
            }

            //防守方真值时钟：只有本机玩家的帐本是真值（死人清账在 TickLocalTruth 里带）
            if (Main.netMode == NetmodeID.MultiplayerClient
                && Main.LocalPlayer?.active == true) {
                Main.LocalPlayer.GetModPlayer<PlayerHackLedger>().TickLocalTruth();
                PlayerHackMirror.Tick();
            }
        }

        public override void OnWorldUnload() => PlayerHackNet.Reset();

        public override void Unload() => PlayerHackNet.Reset();
    }
}

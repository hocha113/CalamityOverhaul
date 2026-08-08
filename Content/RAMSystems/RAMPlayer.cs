using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.RAMSystems
{
    internal sealed class RAMPlayer : ModPlayer
    {
        private const string SaveKey_CapacityChips = "CWRRam_CapacityChips";
        private const string SaveKey_RecoveryChips = "CWRRam_RecoveryChips";
        private const string SaveKey_BaseMax = "CWRRam_BaseMax";
        private const string SaveKey_BaseRecover = "CWRRam_BaseRecover";
        private const int SnapshotInterval = 15;
        private const int ProfileRetryInterval = 120;
        //保底快照间隔（帧）：即使无脏标记也定期发送，杜绝客户端永久滞留过期状态
        private const int KeepaliveInterval = 120;
        //预扣超时（帧），超时未收到回执则回滚本地显示
        private const int PredictionTimeoutFrames = 120;

        private readonly Dictionary<uint, RamRequestResult> recentRequestResults = [];
        private readonly Queue<uint> recentRequestOrder = [];
        //客户端未决预扣：requestId → (金额, 过期帧)，仅本地显示用
        private readonly Dictionary<uint, (float Amount, ulong ExpireFrame)> pendingDebits = [];
        private float pendingDebitTotal;
        //客户端未兑现的芯片扣除：等权威端放行后才动背包，同一时刻只允许一笔在途
        private uint pendingUpgradeRequestId;
        private int pendingUpgradeSlot = -1;
        private ulong pendingUpgradeExpireFrame;

        private int usedCapacityUpgradeChips;
        private int usedRecoveryUpgradeChips;
        private int maxRam = RamSystem.DefaultBaseMaxRam;
        private float recoveryRate = RamSystem.DefaultBaseRecoveryRate;
        private float currentRam = RamSystem.DefaultBaseMaxRam;
        private float recoveryCooldown;
        private int lockTimer;
        private int lockTotalFrames;
        private int flashTimer;
        private int profileRetryTimer;
        private int dirtySyncTimer;
        private int keepaliveTimer;
        private uint nextRequestId;
        private uint highestCompletedRequestId;
        private bool stateDirty;

        public int UsedCapacityUpgradeChips => usedCapacityUpgradeChips;
        public int UsedRecoveryUpgradeChips => usedRecoveryUpgradeChips;
        public int BaseMaxRam => RamSystem.DefaultBaseMaxRam
            + usedCapacityUpgradeChips * RamSystem.CapacityUpgradeChipBonus;
        public float BaseRecoveryRate => RamSystem.DefaultBaseRecoveryRate
            + usedRecoveryUpgradeChips * RamSystem.RecoveryUpgradeChipBonus;
        public int MaxRam => maxRam;
        public float RecoveryRate => recoveryRate;
        /// <summary>对外呈现值 = 权威值 − 客户端未决预扣（权威端预扣恒为 0）</summary>
        public float CurrentRam => pendingDebitTotal > 0f
            ? MathHelper.Clamp(currentRam - pendingDebitTotal, 0f, maxRam)
            : currentRam;
        public float RecoveryCooldown => recoveryCooldown;
        public int LockRemain => lockTimer;
        public int LockTotal => lockTotalFrames;
        public bool IsLocked => lockTimer > 0;
        public bool IsFlashing => flashTimer > 0;
        public bool HasPendingUpgrade => pendingUpgradeRequestId != 0;
        public bool ProfileInitialized { get; private set; }
        public uint SessionId { get; private set; }
        public uint Revision { get; private set; }
        public int DisplayCurrent => (int)CurrentRam;
        public float Ratio => maxRam > 0 ? MathHelper.Clamp(CurrentRam / maxRam, 0f, 1f) : 0f;
        public float LockRemainRatio => lockTimer > 0 && lockTotalFrames > 0
            ? MathHelper.Clamp(lockTimer / (float)lockTotalFrames, 0f, 1f)
            : 0f;

        public override void Initialize() {
            usedCapacityUpgradeChips = 0;
            usedRecoveryUpgradeChips = 0;
            ResetSessionState();
        }

        public override void SaveData(TagCompound tag) {
            tag[SaveKey_CapacityChips] = usedCapacityUpgradeChips;
            tag[SaveKey_RecoveryChips] = usedRecoveryUpgradeChips;
        }

        public override void LoadData(TagCompound tag) {
            if (tag == null) {
                usedCapacityUpgradeChips = 0;
                usedRecoveryUpgradeChips = 0;
            }
            else {
                usedCapacityUpgradeChips = tag.TryGet(SaveKey_CapacityChips, out int cap)
                    ? SanitizeCapacityChipCount(cap)
                    : GetLegacyCapacityChipCount(tag);
                usedRecoveryUpgradeChips = tag.TryGet(SaveKey_RecoveryChips, out int rec)
                    ? SanitizeRecoveryChipCount(rec)
                    : GetLegacyRecoveryChipCount(tag);
            }
            RecomputeEffectiveCore();
            currentRam = maxRam;
        }

        public override void OnEnterWorld() {
            ResetSessionState();
            RecomputeEffectiveCore();
            currentRam = maxRam;

            if (Main.netMode == NetmodeID.SinglePlayer) {
                InitializeAuthorityProfile(usedCapacityUpgradeChips,
                    usedRecoveryUpgradeChips, RamNet.AllocateSessionId());
            }
            else if (Main.netMode == NetmodeID.MultiplayerClient
                && Player.whoAmI == Main.myPlayer) {
                RamNet.SendInitialProfile(this);
            }
        }

        public override void PlayerDisconnect() => ResetSessionState();

        public override void SyncPlayer(int toWho, int fromWho, bool newPlayer) {
            if (Main.netMode == NetmodeID.Server && ProfileInitialized
                && (toWho < 0 || toWho == Player.whoAmI)) {
                RamNet.SendStateSnapshot(Player, Player.whoAmI);
            }
        }

        public override void PostUpdate() {
            if (Player.whoAmI == Main.myPlayer && Main.netMode != NetmodeID.Server
                && flashTimer > 0) {
                flashTimer--;
            }
            //权威 tick 在 RamSystem.Update（PostUpdateEverything）驱动，
            //不挂 PostUpdate：死亡时 Player.Update 提前返回会冻住恢复与锁倒计时
            if (Main.netMode == NetmodeID.MultiplayerClient) {
                ClientUpkeep();
            }
        }

        /// <summary>死亡期间 PostUpdate 不会执行，客户端握手重试/预扣清理仍需推进</summary>
        public override void UpdateDead() {
            if (Main.netMode == NetmodeID.MultiplayerClient) {
                ClientUpkeep();
            }
        }

        private void ClientUpkeep() {
            if (Player.whoAmI != Main.myPlayer) {
                return;
            }
            PurgeExpiredPredictedDebits();
            PurgeExpiredPendingUpgrade();
            RetryInitialProfile();
        }

        /// <summary>服务器/单人权威推进，由 <see cref="RamSystem.Update"/> 每帧驱动（含死亡玩家）</summary>
        internal void UpdateAuthorityTick() {
            if (Main.netMode == NetmodeID.MultiplayerClient || !ProfileInitialized) {
                return;
            }
            UpdateAuthorityState();
            FlushDirtySnapshot();
        }

        internal bool InitializeAuthorityProfile(int capacityChips, int recoveryChips,
            uint sessionId) {
            if (Main.netMode == NetmodeID.MultiplayerClient || ProfileInitialized
                || sessionId == 0) {
                return false;
            }

            usedCapacityUpgradeChips = SanitizeCapacityChipCount(capacityChips);
            usedRecoveryUpgradeChips = SanitizeRecoveryChipCount(recoveryChips);
            SessionId = sessionId;
            Revision = 1;
            ProfileInitialized = true;
            nextRequestId = 0;
            highestCompletedRequestId = 0;
            recentRequestResults.Clear();
            recentRequestOrder.Clear();
            recoveryCooldown = 0f;
            lockTimer = 0;
            lockTotalFrames = 0;
            RecomputeEffectiveCore();
            currentRam = maxRam;
            stateDirty = false;
            dirtySyncTimer = 0;
            return true;
        }

        internal RamStateSnapshot CaptureSnapshot() => new(
            Player.whoAmI,
            SessionId,
            Revision,
            usedCapacityUpgradeChips,
            usedRecoveryUpgradeChips,
            maxRam,
            currentRam,
            recoveryRate,
            recoveryCooldown,
            lockTimer,
            lockTotalFrames);

        internal bool ApplySnapshot(in RamStateSnapshot snapshot) {
            if (!snapshot.IsValid || snapshot.PlayerIndex != Player.whoAmI) {
                return false;
            }
            //同会话内旧版本丢弃；会话不同则直接收养——自身快照只可能来自
            //服务器权威，硬拒会在会话漂移后永久卡死本地显示
            if (ProfileInitialized && snapshot.SessionId == SessionId
                && !IsRevisionAtLeast(snapshot.Revision, Revision)) {
                return false;
            }

            bool newSession = !ProfileInitialized || snapshot.SessionId != SessionId;
            ProfileInitialized = true;
            SessionId = snapshot.SessionId;
            Revision = snapshot.Revision;
            usedCapacityUpgradeChips = snapshot.CapacityChips;
            usedRecoveryUpgradeChips = snapshot.RecoveryChips;
            maxRam = snapshot.MaxRam;
            currentRam = snapshot.CurrentRam;
            recoveryRate = snapshot.RecoveryRate;
            recoveryCooldown = snapshot.RecoveryCooldown;
            lockTimer = snapshot.LockRemain;
            lockTotalFrames = snapshot.LockTotal;
            stateDirty = false;
            dirtySyncTimer = 0;
            profileRetryTimer = 0;

            if (newSession) {
                nextRequestId = 0;
                highestCompletedRequestId = 0;
                recentRequestResults.Clear();
                recentRequestOrder.Clear();
                ClearPredictedDebits();
                ClearPendingUpgrade();
            }
            return true;
        }

        internal bool CanAfford(float amount) {
            //用呈现值判断，客户端把未决预扣也计入，防止 RTT 窗口内超发
            return IsValidMutationAmount(amount) && !IsLocked && CurrentRam >= amount;
        }

        internal bool TryConsumeAuthority(float amount, out float paid) {
            paid = 0f;
            if (!CanAfford(amount) || Main.netMode == NetmodeID.MultiplayerClient) {
                return false;
            }
            if (amount <= 0f) {
                return true;
            }

            float before = currentRam;
            currentRam = MathHelper.Clamp(currentRam - amount, 0f, maxRam);
            recoveryCooldown = RamSystem.RecoveryDelay;
            paid = before - currentRam;
            CommitStateChange(immediate: true);
            return true;
        }

        internal bool TryConsumeOverTimeAuthority(float ramPerSecond, out float paid) {
            paid = 0f;
            if (Main.netMode == NetmodeID.MultiplayerClient || IsLocked
                || !float.IsFinite(ramPerSecond) || ramPerSecond <= 0f
                || ramPerSecond > RamSystem.MaxMutationPerSecond) {
                return false;
            }

            float before = currentRam;
            currentRam = MathHelper.Clamp(currentRam - ramPerSecond / 60f, 0f, maxRam);
            paid = before - currentRam;
            if (paid <= 0f) {
                return true;
            }

            bool depleted = before > 0f && currentRam <= 0f;
            CommitStateChange(immediate: depleted);
            return true;
        }

        internal bool RestoreAuthority(float amount, out float restored) {
            restored = 0f;
            if (Main.netMode == NetmodeID.MultiplayerClient || IsLocked
                || !IsValidMutationAmount(amount) || amount <= 0f) {
                return false;
            }

            float before = currentRam;
            currentRam = MathHelper.Clamp(currentRam + amount, 0f, maxRam);
            restored = currentRam - before;
            if (restored > 0f) {
                CommitStateChange(immediate: true);
            }
            return true;
        }

        internal bool RefillAuthority() {
            if (Main.netMode == NetmodeID.MultiplayerClient || IsLocked) {
                return false;
            }
            bool changed = currentRam != maxRam || recoveryCooldown > 0f;
            currentRam = maxRam;
            recoveryCooldown = 0f;
            if (changed) {
                CommitStateChange(immediate: true);
            }
            return true;
        }

        internal bool SetLockAuthority(int frames) {
            if (Main.netMode == NetmodeID.MultiplayerClient || frames <= 0
                || frames > RamSystem.MaxLockFrames) {
                return false;
            }

            lockTimer = frames;
            lockTotalFrames = frames;
            currentRam = 0f;
            recoveryCooldown = 0f;
            CommitStateChange(immediate: true);
            return true;
        }

        internal bool ClearLockAuthority() {
            if (Main.netMode == NetmodeID.MultiplayerClient) {
                return false;
            }
            if (lockTimer <= 0 && lockTotalFrames <= 0) {
                return true;
            }
            lockTimer = 0;
            lockTotalFrames = 0;
            CommitStateChange(immediate: true);
            return true;
        }

        internal bool CanUseUpgrade(RamUpgradeKind kind) {
            return kind switch {
                RamUpgradeKind.Capacity => usedCapacityUpgradeChips < RamSystem.MaxCapacityUpgradeChips,
                RamUpgradeKind.Recovery => usedRecoveryUpgradeChips < RamSystem.MaxRecoveryUpgradeChips,
                _ => false,
            };
        }

        internal bool TryUseUpgradeAuthority(RamUpgradeKind kind) {
            if (Main.netMode == NetmodeID.MultiplayerClient || !CanUseUpgrade(kind)) {
                return false;
            }

            int oldMax = maxRam;
            if (kind == RamUpgradeKind.Capacity) {
                usedCapacityUpgradeChips++;
            }
            else if (kind == RamUpgradeKind.Recovery) {
                usedRecoveryUpgradeChips++;
            }
            else {
                return false;
            }

            RecomputeEffectiveCore();
            if (kind == RamUpgradeKind.Capacity) {
                currentRam = MathHelper.Clamp(currentRam + Math.Max(maxRam - oldMax, 0), 0f, maxRam);
            }
            CommitStateChange(immediate: true);
            return true;
        }

        /// <summary>
        /// 登记一笔待兑现的芯片扣除。非 ServerSideCharacter 的联机里背包归本机管，
        /// 服务端发来的自身槽位同步会被原版丢弃，所以芯片只能等权威端放行后由本机扣，
        /// 改完的槽位由原版每帧的 TrySyncingMyPlayer 回灌服务端
        /// </summary>
        internal void RegisterPendingUpgrade(uint requestId, int inventorySlot) {
            if (Main.netMode != NetmodeID.MultiplayerClient
                || Player.whoAmI != Main.myPlayer || requestId == 0) {
                return;
            }
            pendingUpgradeRequestId = requestId;
            pendingUpgradeSlot = inventorySlot;
            pendingUpgradeExpireFrame = Main.GameUpdateCount + PredictionTimeoutFrames;
        }

        /// <summary>取走该请求登记的槽位，超时清理过的回执只会拿到 -1 兜底</summary>
        internal bool TryTakePendingUpgrade(uint requestId, out int inventorySlot) {
            inventorySlot = -1;
            if (pendingUpgradeRequestId == 0 || pendingUpgradeRequestId != requestId) {
                return false;
            }
            inventorySlot = pendingUpgradeSlot;
            ClearPendingUpgrade();
            return true;
        }

        private void ClearPendingUpgrade() {
            pendingUpgradeRequestId = 0;
            pendingUpgradeSlot = -1;
            pendingUpgradeExpireFrame = 0;
        }

        /// <summary>超时只解开连点门，回执迟到时仍按操作类型兜底扣除</summary>
        private void PurgeExpiredPendingUpgrade() {
            if (pendingUpgradeRequestId != 0
                && Main.GameUpdateCount >= pendingUpgradeExpireFrame) {
                ClearPendingUpgrade();
            }
        }

        /// <summary>客户端登记一笔预扣，回执或超时后对账</summary>
        internal void RegisterPredictedDebit(uint requestId, float amount) {
            if (Main.netMode != NetmodeID.MultiplayerClient
                || Player.whoAmI != Main.myPlayer || requestId == 0
                || !float.IsFinite(amount) || amount <= 0f) {
                return;
            }
            pendingDebits[requestId] = (Math.Min(amount, RamSystem.MaxMutationAmount),
                Main.GameUpdateCount + PredictionTimeoutFrames);
            RecomputePendingDebitTotal();
        }

        /// <summary>收到该请求的权威回执，撤销对应预扣</summary>
        internal void SettlePredictedDebit(uint requestId) {
            if (pendingDebits.Remove(requestId)) {
                RecomputePendingDebitTotal();
            }
        }

        private void ClearPredictedDebits() {
            if (pendingDebits.Count == 0 && pendingDebitTotal == 0f) {
                return;
            }
            pendingDebits.Clear();
            pendingDebitTotal = 0f;
        }

        private void PurgeExpiredPredictedDebits() {
            if (pendingDebits.Count == 0) {
                return;
            }
            ulong now = Main.GameUpdateCount;
            List<uint> expired = null;
            foreach (KeyValuePair<uint, (float Amount, ulong ExpireFrame)> pair in pendingDebits) {
                if (now >= pair.Value.ExpireFrame) {
                    (expired ??= []).Add(pair.Key);
                }
            }
            if (expired == null) {
                return;
            }
            for (int i = 0; i < expired.Count; i++) {
                pendingDebits.Remove(expired[i]);
            }
            RecomputePendingDebitTotal();
        }

        private void RecomputePendingDebitTotal() {
            float total = 0f;
            foreach (KeyValuePair<uint, (float Amount, ulong ExpireFrame)> pair in pendingDebits) {
                total += pair.Value.Amount;
            }
            pendingDebitTotal = MathHelper.Clamp(total, 0f, RamSystem.MaxMutationAmount);
        }

        internal void NotifyInsufficient() => flashTimer = RamSystem.InsufficientFlashFrames;

        internal float GetWarningPulse() {
            if (IsLocked) {
                return 1f;
            }
            return flashTimer > 0
                ? MathHelper.Clamp(flashTimer / (float)RamSystem.InsufficientFlashFrames, 0f, 1f)
                : 0f;
        }

        internal bool TryAllocateRequest(out RamRequestToken token) {
            token = default;
            if (!ProfileInitialized || SessionId == 0) {
                return false;
            }
            do {
                nextRequestId++;
            }
            while (nextRequestId == 0 || recentRequestResults.ContainsKey(nextRequestId));
            token = new RamRequestToken(SessionId, nextRequestId);
            return true;
        }

        internal RamRequestDisposition ClassifyRequest(uint sessionId, uint requestId,
            ushort operationId, out RamRequestResult previous) {
            previous = default;
            if (!ProfileInitialized || sessionId == 0 || sessionId != SessionId
                || requestId == 0 || operationId == 0) {
                return RamRequestDisposition.Invalid;
            }
            if (!recentRequestResults.TryGetValue(requestId, out previous)) {
                return highestCompletedRequestId == 0
                    || IsRevisionNewer(requestId, highestCompletedRequestId)
                    ? RamRequestDisposition.New
                    : RamRequestDisposition.Expired;
            }
            return previous.OperationId == operationId
                ? RamRequestDisposition.Replay
                : RamRequestDisposition.Conflict;
        }

        internal void StoreRequestResult(in RamRequestResult result) {
            if (!result.IsValid || result.SessionId != SessionId) {
                return;
            }
            if (recentRequestResults.ContainsKey(result.RequestId)) {
                recentRequestResults[result.RequestId] = result;
                return;
            }
            while (recentRequestResults.Count >= RamSystem.MaxRecentRequestResults
                && recentRequestOrder.TryDequeue(out uint expired)) {
                recentRequestResults.Remove(expired);
            }
            recentRequestResults[result.RequestId] = result;
            recentRequestOrder.Enqueue(result.RequestId);
            if (highestCompletedRequestId == 0
                || IsRevisionNewer(result.RequestId, highestCompletedRequestId)) {
                highestCompletedRequestId = result.RequestId;
            }
        }

        internal bool TryGetRequestResult(uint requestId, out RamRequestResult result)
            => recentRequestResults.TryGetValue(requestId, out result);

        internal void MarkSnapshotSent() {
            stateDirty = false;
            dirtySyncTimer = 0;
            keepaliveTimer = 0;
        }

        private void UpdateAuthorityState() {
            bool changed = RecomputeEffectiveCore();
            bool immediate = changed;

            if (lockTimer > 0) {
                lockTimer--;
                currentRam = 0f;
                recoveryCooldown = 0f;
                changed = true;
                if (lockTimer == 0) {
                    lockTotalFrames = 0;
                    immediate = true;
                }
            }
            else if (recoveryCooldown > 0f) {
                recoveryCooldown = Math.Max(0f, recoveryCooldown - 1f / 60f);
                changed = true;
            }
            else if (currentRam < maxRam && recoveryRate > 0f) {
                currentRam = MathHelper.Clamp(currentRam + recoveryRate / 60f, 0f, maxRam);
                changed = true;
            }

            if (changed) {
                CommitStateChange(immediate);
            }
        }

        private bool RecomputeEffectiveCore() {
            int newMax = BaseMaxRam;
            float newRecovery = BaseRecoveryRate;
            IReadOnlyList<IRamModifierProvider> providers = RamSystem.ModifierProviders;
            for (int i = 0; i < providers.Count; i++) {
                IRamModifierProvider provider = providers[i];
                if (!RamSystem.TryGetProviderBonuses(provider, Player,
                    out int maxBonus, out float recoveryBonus)) {
                    continue;
                }
                newMax += maxBonus;
                newRecovery += recoveryBonus;
            }

            newMax = Math.Clamp(newMax, RamSystem.MinBaseMaxRam, RamSystem.SoftMaxBaseMaxRam);
            newRecovery = float.IsFinite(newRecovery)
                ? MathHelper.Clamp(newRecovery, 0f, RamSystem.MaxEffectiveRecoveryRate)
                : 0f;
            bool changed = newMax != maxRam || MathF.Abs(newRecovery - recoveryRate) > 0.0001f;
            maxRam = newMax;
            recoveryRate = newRecovery;
            if (!float.IsFinite(currentRam)) {
                currentRam = 0f;
                changed = true;
            }
            currentRam = MathHelper.Clamp(currentRam, 0f, maxRam);
            return changed;
        }

        private void CommitStateChange(bool immediate) {
            if (!ProfileInitialized) {
                return;
            }
            Revision++;
            if (Revision == 0) {
                Revision = 1;
            }
            stateDirty = true;
            if (immediate && Main.netMode == NetmodeID.Server) {
                RamNet.SendStateSnapshot(Player, Player.whoAmI);
            }
        }

        private void FlushDirtySnapshot() {
            if (Main.netMode != NetmodeID.Server) {
                return;
            }
            keepaliveTimer++;
            bool dirtyDue = stateDirty && ++dirtySyncTimer >= SnapshotInterval;
            if (dirtyDue || keepaliveTimer >= KeepaliveInterval) {
                RamNet.SendStateSnapshot(Player, Player.whoAmI);
            }
        }

        private void RetryInitialProfile() {
            if (Player.whoAmI != Main.myPlayer || ProfileInitialized) {
                return;
            }
            if (++profileRetryTimer >= ProfileRetryInterval) {
                profileRetryTimer = 0;
                RamNet.SendInitialProfile(this);
            }
        }

        private void ResetSessionState() {
            ProfileInitialized = false;
            SessionId = 0;
            Revision = 0;
            nextRequestId = 0;
            highestCompletedRequestId = 0;
            recentRequestResults.Clear();
            recentRequestOrder.Clear();
            recoveryCooldown = 0f;
            lockTimer = 0;
            lockTotalFrames = 0;
            flashTimer = 0;
            profileRetryTimer = 0;
            dirtySyncTimer = 0;
            stateDirty = false;
            currentRam = maxRam;
            ClearPredictedDebits();
            ClearPendingUpgrade();
        }

        private static int SanitizeCapacityChipCount(int value)
            => Math.Clamp(value, 0, RamSystem.MaxCapacityUpgradeChips);

        private static int SanitizeRecoveryChipCount(int value)
            => Math.Clamp(value, 0, RamSystem.MaxRecoveryUpgradeChips);

        private static bool IsValidMutationAmount(float amount)
            => float.IsFinite(amount) && amount >= 0f && amount <= RamSystem.MaxMutationAmount;

        private static bool IsRevisionAtLeast(uint candidate, uint baseline)
            => candidate == baseline || unchecked((int)(candidate - baseline)) > 0;

        private static bool IsRevisionNewer(uint candidate, uint baseline)
            => unchecked((int)(candidate - baseline)) > 0;

        private static int GetLegacyCapacityChipCount(TagCompound tag) {
            if (!tag.TryGet(SaveKey_BaseMax, out int max)) {
                return 0;
            }
            return SanitizeCapacityChipCount(max - RamSystem.DefaultBaseMaxRam);
        }

        private static int GetLegacyRecoveryChipCount(TagCompound tag) {
            if (!tag.TryGet(SaveKey_BaseRecover, out float rec) || !float.IsFinite(rec)) {
                return 0;
            }
            int count = (int)MathF.Round((rec - RamSystem.DefaultBaseRecoveryRate)
                / RamSystem.RecoveryUpgradeChipBonus);
            return SanitizeRecoveryChipCount(count);
        }
    }
}

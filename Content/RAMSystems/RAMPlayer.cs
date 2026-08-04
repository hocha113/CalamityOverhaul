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

        private readonly Dictionary<uint, RamRequestResult> recentRequestResults = [];
        private readonly Queue<uint> recentRequestOrder = [];

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
        public float CurrentRam => currentRam;
        public float RecoveryCooldown => recoveryCooldown;
        public int LockRemain => lockTimer;
        public int LockTotal => lockTotalFrames;
        public bool IsLocked => lockTimer > 0;
        public bool IsFlashing => flashTimer > 0;
        public bool ProfileInitialized { get; private set; }
        public uint SessionId { get; private set; }
        public uint Revision { get; private set; }
        public int DisplayCurrent => (int)currentRam;
        public float Ratio => maxRam > 0 ? MathHelper.Clamp(currentRam / maxRam, 0f, 1f) : 0f;
        public float LockRemainRatio => lockTimer > 0 && lockTotalFrames > 0
            ? MathHelper.Clamp(lockTimer / (float)lockTotalFrames, 0f, 1f)
            : 0f;

        internal event Action OnDepleted;

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

            if (Main.netMode == NetmodeID.MultiplayerClient) {
                RetryInitialProfile();
                return;
            }
            if (!ProfileInitialized) {
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
            if (ProfileInitialized && snapshot.SessionId != SessionId) {
                return false;
            }
            if (ProfileInitialized && snapshot.SessionId == SessionId
                && !IsRevisionAtLeast(snapshot.Revision, Revision)) {
                return false;
            }

            bool depleted = currentRam > 0f && snapshot.CurrentRam <= 0f;
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
            }
            if (depleted) {
                OnDepleted?.Invoke();
            }
            return true;
        }

        internal bool CanAfford(float amount) {
            return IsValidMutationAmount(amount) && !IsLocked && currentRam >= amount;
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
            if (before > 0f && currentRam <= 0f) {
                OnDepleted?.Invoke();
            }
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
            if (depleted) {
                OnDepleted?.Invoke();
            }
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

            bool depleted = currentRam > 0f;
            lockTimer = frames;
            lockTotalFrames = frames;
            currentRam = 0f;
            recoveryCooldown = 0f;
            CommitStateChange(immediate: true);
            if (depleted) {
                OnDepleted?.Invoke();
            }
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
            if (!stateDirty || Main.netMode != NetmodeID.Server) {
                return;
            }
            if (++dirtySyncTimer >= SnapshotInterval) {
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

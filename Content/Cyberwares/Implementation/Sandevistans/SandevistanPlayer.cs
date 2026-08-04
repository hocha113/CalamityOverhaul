using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Cyberwares;
using CalamityOverhaul.Content.TimeFreezes;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Cyberwares.Implementation.Sandevistans
{
    internal sealed class SandevistanPlayer : ModPlayer
    {
        internal const int SnapshotInterval = 15;
        internal const int RecoveryDelayTicks = 120;
        internal const int SpawnInterval = 4;
        internal const float DefaultSlowFactor = 0.08f;
        internal const float MaxSlowFactor = 1f;
        internal const float MaxRate = 100f;
        internal const float MaxCooldownValue = 6000f;
        private const int MaxRecentRequests = 32;
        private const int RequestWindowFrames = 60;
        private const int MaxRequestsPerWindow = 12;

        private readonly Dictionary<uint, bool> recentRequests = [];
        private readonly Queue<uint> recentRequestOrder = [];
        private int equippedType = ItemID.None;
        private int recoveryDelay;
        private int spawnTimer;
        private int snapshotTimer;
        private int requestWindowCount;
        private bool stateDirty;
        private bool wasActive;
        private uint nextRequestId;
        private uint highestRequestId;
        private ulong requestWindowStart;

        public bool IsActive { get; private set; }
        public float CurrentCooldown { get; private set; }
        public float MaxCooldown { get; private set; }
        public float ConsumptionRate { get; private set; }
        public float RecoveryRate { get; private set; }
        public float SlowFactor { get; private set; } = DefaultSlowFactor;
        public float ScreenEffectIntensity { get; private set; }
        public uint SessionGeneration { get; private set; }
        public uint StateRevision { get; private set; }
        public int EquippedType => equippedType;
        internal int RecoveryDelay => recoveryDelay;

        internal bool HasValidEquipment
            => equippedType != ItemID.None && MaxCooldown > 0f
            && float.IsFinite(SlowFactor) && SlowFactor > 0f && SlowFactor <= 1f;

        internal bool EligibleForAggregate
            => IsActive && HasValidEquipment && Player?.active == true
            && !Player.dead && !Player.ghost;

        public override void Initialize() {
            ResetRuntime();
        }

        public override void OnEnterWorld() {
            ResetRuntime();
        }

        public override void PlayerDisconnect() {
            ResetRuntime();
        }

        public override void UpdateDead() {
            if (Main.netMode != NetmodeID.MultiplayerClient) {
                SetAuthorityActive(false);
            }
            else {
                IsActive = false;
            }
        }

        public override void PostUpdate() {
            SyncSessionAndEquipment();

            if (Main.netMode != NetmodeID.MultiplayerClient) {
                TickAuthority();
            }

            UpdateVisuals();

            if (Main.netMode == NetmodeID.Server && ProfileReady()) {
                snapshotTimer++;
                if (stateDirty || snapshotTimer >= SnapshotInterval) {
                    snapshotTimer = 0;
                    stateDirty = false;
                    SandevistanNet.SendState(this);
                }
            }
        }

        public override void SyncPlayer(int toWho, int fromWho, bool newPlayer) {
            if (Main.netMode == NetmodeID.Server && ProfileReady()) {
                SandevistanNet.SendState(this, toWho, fromWho);
                SandevistanNet.SendAggregate(toWho, fromWho);
            }
        }

        internal bool RequestToggle(bool desiredActive) {
            if (!ProfileReady() || !HasValidEquipment || Player.dead || Player.ghost) {
                return false;
            }

            if (Main.netMode == NetmodeID.SinglePlayer) {
                return SetAuthorityActive(desiredActive);
            }
            if (Main.netMode != NetmodeID.MultiplayerClient
                || Player.whoAmI != Main.myPlayer) {
                return false;
            }

            uint requestId = AllocateRequestId();
            return requestId != 0 && SandevistanNet.SendToggleRequest(this,
                desiredActive, requestId);
        }

        internal uint AllocateRequestId() {
            do {
                nextRequestId++;
            }
            while (nextRequestId == 0 || recentRequests.ContainsKey(nextRequestId));
            return nextRequestId;
        }

        internal bool HandleAuthorityRequest(uint sessionGeneration,
            uint requestId, uint expectedRevision, bool desiredActive, int replyTo) {
            SyncSessionAndEquipment();
            if (Main.netMode == NetmodeID.MultiplayerClient || !ProfileReady()
                || requestId == 0 || sessionGeneration == 0
                || sessionGeneration != SessionGeneration) {
                SandevistanNet.SendState(this, replyTo);
                return false;
            }

            if (recentRequests.ContainsKey(requestId)) {
                SandevistanNet.SendState(this, replyTo);
                return false;
            }
            if (highestRequestId != 0
                && !CyberwarePlayer.IsRevisionNewer(requestId, highestRequestId)) {
                SandevistanNet.SendState(this, replyTo);
                return false;
            }
            RememberRequest(requestId, desiredActive);
            if (!AllowAuthorityRequest()) {
                SandevistanNet.SendState(this, replyTo);
                return false;
            }

            if (expectedRevision == 0 || expectedRevision != StateRevision
                || !HasValidEquipment || Player.dead || Player.ghost) {
                SandevistanNet.SendState(this, replyTo);
                return false;
            }

            if (desiredActive && CurrentCooldown <= 0f) {
                SandevistanNet.SendState(this, replyTo);
                return false;
            }

            bool changed = SetAuthorityActive(desiredActive);
            SandevistanNet.SendState(this);
            return changed;
        }

        internal void ApplySnapshot(uint sessionGeneration, uint revision,
            int itemType, bool active, float currentCooldown, float maxCooldown,
            float consumptionRate, float recoveryRate, float slowFactor,
            int recoveryDelay) {
            if (Main.netMode != NetmodeID.MultiplayerClient
                || sessionGeneration == 0 || revision == 0
                || itemType < ItemID.None || itemType >= ItemLoader.ItemCount
                || !IsFiniteBounded(slowFactor, 0.001f, MaxSlowFactor)
                || recoveryDelay < 0 || recoveryDelay > RecoveryDelayTicks) {
                return;
            }

            bool empty = itemType == ItemID.None;
            if (empty) {
                if (active || currentCooldown != 0f || maxCooldown != 0f
                    || consumptionRate != 0f || recoveryRate != 0f
                    || recoveryDelay != 0) {
                    return;
                }
            }
            else if (!IsFiniteBounded(currentCooldown, 0f, MaxCooldownValue)
                || !IsFiniteBounded(maxCooldown, 0.01f, MaxCooldownValue)
                || currentCooldown > maxCooldown
                || active && currentCooldown <= 0f
                || !IsFiniteBounded(consumptionRate, 0.001f, MaxRate)
                || !IsFiniteBounded(recoveryRate, 0f, MaxRate)) {
                return;
            }

            if (SessionGeneration != 0) {
                if (sessionGeneration != SessionGeneration
                    && !CyberwarePlayer.IsRevisionNewer(sessionGeneration,
                        SessionGeneration)) {
                    return;
                }
                if (sessionGeneration == SessionGeneration && StateRevision != 0
                    && !CyberwarePlayer.IsRevisionNewer(revision, StateRevision)
                    && revision != StateRevision) {
                    return;
                }
            }

            bool oldActive = IsActive;
            if (sessionGeneration != SessionGeneration) {
                ResetRuntime();
                SessionGeneration = sessionGeneration;
            }
            StateRevision = revision;
            equippedType = itemType;
            IsActive = active;
            CurrentCooldown = Math.Clamp(currentCooldown, 0f, maxCooldown);
            MaxCooldown = maxCooldown;
            ConsumptionRate = consumptionRate;
            RecoveryRate = recoveryRate;
            SlowFactor = slowFactor;
            this.recoveryDelay = recoveryDelay;
            stateDirty = false;

            if (oldActive != IsActive && Player.whoAmI == Main.myPlayer) {
                SoundEngine.PlaySound(IsActive
                    ? CWRSound.SandevistanStart
                    : CWRSound.SandevistanEnd, Player.Center);
            }
            wasActive = IsActive;
        }

        internal void SetLegacyCooldown(float value) {
            if (Main.netMode == NetmodeID.MultiplayerClient) {
                return;
            }
            if (!float.IsFinite(value)) {
                return;
            }
            CurrentCooldown = Math.Clamp(value, 0f,
                MaxCooldown > 0f ? MaxCooldown : MaxCooldownValue);
            MarkStateDirty();
        }

        internal void DeactivateForEquipmentChange() {
            if (Main.netMode == NetmodeID.MultiplayerClient) {
                IsActive = false;
                return;
            }
            SetAuthorityActive(false);
        }

        internal void ResetForEquipmentChange() {
            equippedType = ItemID.None;
            MaxCooldown = 0f;
            ConsumptionRate = 0f;
            RecoveryRate = 0f;
            SlowFactor = DefaultSlowFactor;
            CurrentCooldown = 0f;
            recoveryDelay = 0;
            SetAuthorityActive(false);
        }

        private bool SetAuthorityActive(bool active) {
            if (Main.netMode == NetmodeID.MultiplayerClient) {
                IsActive = active && HasValidEquipment && !Player.dead && !Player.ghost;
                return IsActive == active;
            }
            if (active && (!HasValidEquipment || Player?.active != true
                || Player.dead || Player.ghost || CurrentCooldown <= 0f)) {
                active = false;
            }
            if (IsActive == active) {
                return false;
            }
            IsActive = active;
            if (active) {
                recoveryDelay = RecoveryDelayTicks;
            }
            MarkStateDirty();
            return true;
        }

        private void TickAuthority() {
            if (!ProfileReady() || !HasValidEquipment || Player.dead || Player.ghost) {
                SetAuthorityActive(false);
                return;
            }

            float externalScale = TimeGear.TimeScaleExcluding<SandevistanTimeSlow>();
            if (!float.IsFinite(externalScale)) {
                externalScale = 1f;
            }
            externalScale = Math.Clamp(externalScale, 0f, 1f);

            if (IsActive) {
                CurrentCooldown = Math.Clamp(CurrentCooldown
                    - ConsumptionRate * externalScale, 0f, MaxCooldown);
                recoveryDelay = RecoveryDelayTicks;
                if (CurrentCooldown <= 0f) {
                    SetAuthorityActive(false);
                }
            }
            else if (externalScale > 0f) {
                if (recoveryDelay > 0) {
                    recoveryDelay--;
                }
                else {
                    CurrentCooldown = Math.Clamp(CurrentCooldown
                        + RecoveryRate * externalScale, 0f, MaxCooldown);
                }
            }
            if (!float.IsFinite(CurrentCooldown)) {
                CurrentCooldown = 0f;
                SetAuthorityActive(false);
            }
        }

        private void SyncSessionAndEquipment() {
            CyberwarePlayer cyberware = Player.GetModPlayer<CyberwarePlayer>();
            uint session = cyberware?.SessionGeneration ?? 0;
            if (session != 0 && session != SessionGeneration) {
                if (Main.netMode == NetmodeID.MultiplayerClient
                    && SessionGeneration != 0
                    && !CyberwarePlayer.IsRevisionNewer(session,
                        SessionGeneration)) {
                    return;
                }
                ResetRuntime();
                SessionGeneration = session;
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    StateRevision = 1;
                }
            }

            SandevistansItem equipped = Sandevistan.GetEquipped(Player);
            int type = equipped?.Item?.type ?? ItemID.None;
            if (type == equippedType) {
                return;
            }

            equippedType = type;
            if (equipped == null) {
                MaxCooldown = 0f;
                ConsumptionRate = 0f;
                RecoveryRate = 0f;
                SlowFactor = DefaultSlowFactor;
                CurrentCooldown = 0f;
                recoveryDelay = 0;
                SetAuthorityActive(false);
                return;
            }

            MaxCooldown = ClampValue(equipped.MaxCooldownTime, 0.01f,
                MaxCooldownValue, 300f);
            ConsumptionRate = ClampValue(equipped.ConsumptionPerFrame, 0.001f,
                MaxRate, 2f);
            RecoveryRate = ClampValue(equipped.RecoveryPerFrame, 0f,
                MaxRate, 0.4f);
            SlowFactor = ClampValue(equipped.SlowFactor, 0.001f,
                MaxSlowFactor, DefaultSlowFactor);
            if (Main.netMode != NetmodeID.MultiplayerClient
                || CurrentCooldown <= 0f || StateRevision == 0) {
                CurrentCooldown = MaxCooldown;
            }
            SetAuthorityActive(false);
            if (Main.netMode != NetmodeID.MultiplayerClient) {
                MarkStateDirty();
            }
        }

        private void UpdateVisuals() {
            bool active = IsActive && HasValidEquipment && Player?.active == true
                && !Player.dead && !Player.ghost;
            if (active) {
                ScreenEffectIntensity = Math.Min(ScreenEffectIntensity + 0.05f, 1f);
            }
            else {
                ScreenEffectIntensity = Math.Max(ScreenEffectIntensity - 0.01f, 0f);
            }

            if (active != wasActive && !Main.dedServ
                && Player.whoAmI == Main.myPlayer) {
                SoundEngine.PlaySound(active
                    ? CWRSound.SandevistanStart
                    : CWRSound.SandevistanEnd, Player.Center);
            }
            wasActive = active;

            if (Main.dedServ || Player.whoAmI != Main.myPlayer || !active) {
                spawnTimer = 0;
                return;
            }

            float externalScale = TimeGear.TimeScaleExcluding<SandevistanTimeSlow>();
            if (!float.IsFinite(externalScale) || externalScale <= 0f
                || Player.velocity.LengthSquared() < 1f) {
                return;
            }

            spawnTimer++;
            if (spawnTimer >= SpawnInterval) {
                spawnTimer = 0;
                Sandevistan.SpawnGhost(Player);
            }
        }

        private bool ProfileReady() {
            CyberwarePlayer cyberware = Player?.GetModPlayer<CyberwarePlayer>();
            return Player?.active == true && cyberware?.ProfileInitialized == true
                && SessionGeneration != 0 && StateRevision != 0;
        }

        private void RememberRequest(uint requestId, bool desiredActive) {
            recentRequests[requestId] = desiredActive;
            recentRequestOrder.Enqueue(requestId);
            while (recentRequestOrder.Count > MaxRecentRequests
                && recentRequestOrder.TryDequeue(out uint expired)) {
                recentRequests.Remove(expired);
            }
            highestRequestId = requestId;
        }

        private bool AllowAuthorityRequest() {
            ulong now = Main.GameUpdateCount;
            if (now - requestWindowStart >= RequestWindowFrames) {
                requestWindowStart = now;
                requestWindowCount = 0;
            }
            if (requestWindowCount >= MaxRequestsPerWindow) {
                return false;
            }
            requestWindowCount++;
            return true;
        }

        private void MarkStateDirty() {
            StateRevision++;
            if (StateRevision == 0) {
                StateRevision = 1;
            }
            stateDirty = true;
        }

        private void ResetRuntime() {
            IsActive = false;
            CurrentCooldown = 0f;
            MaxCooldown = 0f;
            ConsumptionRate = 0f;
            RecoveryRate = 0f;
            SlowFactor = DefaultSlowFactor;
            ScreenEffectIntensity = 0f;
            SessionGeneration = 0;
            StateRevision = 0;
            equippedType = ItemID.None;
            recoveryDelay = 0;
            spawnTimer = 0;
            snapshotTimer = 0;
            requestWindowCount = 0;
            stateDirty = false;
            wasActive = false;
            nextRequestId = 0;
            highestRequestId = 0;
            requestWindowStart = Main.GameUpdateCount;
            recentRequests.Clear();
            recentRequestOrder.Clear();
        }

        private static float ClampValue(float value, float min, float max,
            float fallback) {
            return float.IsFinite(value)
                ? Math.Clamp(value, min, max)
                : fallback;
        }

        private static bool IsFiniteBounded(float value, float min, float max)
            => float.IsFinite(value) && value >= min && value <= max;
    }
}

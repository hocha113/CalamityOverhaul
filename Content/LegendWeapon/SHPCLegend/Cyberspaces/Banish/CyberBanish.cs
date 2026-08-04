using CalamityOverhaul.Common;
using CalamityOverhaul.Content.HackTimes;
using CalamityOverhaul.Content.RAMSystems;
using CalamityOverhaul.Content.TimeFreezes;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces.Banish
{
    /// <summary>赛博放逐的权威状态与冻结租约</summary>
    internal partial class CyberBanish : ICWRLoader
    {
        public const int BanishDuration = 108;
        public const int RamCostPerCast = 5;

        private const float BossExecutionThreshold = 0.7f;
        private static readonly List<NPC> groupBuffer = [];
        private static readonly List<BanishActivation> activeActivations = [];
        private static long nextActivationId;

        public static readonly List<BanishEntry> ActiveBanishments = [];

        void ICWRLoader.UnLoadData() => Reset();

        public static bool IsBanishing(int npcIndex)
            => TryGetEntry(npcIndex, out _);

        internal static bool IsBanishing(NetworkNPCIdentity identity) {
            for (int i = 0; i < ActiveBanishments.Count; i++) {
                BanishEntry entry = ActiveBanishments[i];
                if (entry.Identity == identity && IsEntryActive(entry)) {
                    return true;
                }
            }
            return false;
        }

        internal static bool TryGetEntry(int npcIndex, out BanishEntry result) {
            result = null;
            if (npcIndex < 0 || npcIndex >= Main.maxNPCs) {
                return false;
            }
            for (int i = 0; i < ActiveBanishments.Count; i++) {
                BanishEntry entry = ActiveBanishments[i];
                if (entry.NpcIndex == npcIndex && IsEntryActive(entry)) {
                    result = entry;
                    return true;
                }
            }
            return false;
        }

        public static float GetProgress(int npcIndex)
            => TryGetEntry(npcIndex, out BanishEntry entry) ? entry.Progress : -1f;

        public static void BanishAtCursor() {
            Player owner = Main.LocalPlayer;
            CyberspacePlayer cyberspace = Cyberspace.For(owner);
            if (owner?.active != true || owner.dead || cyberspace == null
                || !cyberspace.Active || cyberspace.Intensity < 0.5f
                || cyberspace.CurrentLayer < 2) {
                return;
            }

            int targetIndex = FindCursorTarget(cyberspace);
            if (targetIndex < 0) {
                return;
            }
            NPC target = Main.npc[targetIndex];
            if (!NetworkNPCIdentity.TryCapture(target,
                out NetworkNPCIdentity identity)) {
                return;
            }

            if (Main.netMode == NetmodeID.MultiplayerClient) {
                if (!RamSystem.TryAllocateRequest(owner,
                    out RamRequestToken request)) {
                    PlayRequestFailure(owner);
                    return;
                }
                SendBanishRequest(request, identity);
                return;
            }

            ExecuteAuthoritativeBanish(owner, default, identity, -1);
        }

        private static int FindCursorTarget(CyberspacePlayer cyberspace) {
            Vector2 mouse = Main.MouseWorld;
            Vector2 center = cyberspace.DomainCenter;
            float radius = cyberspace.Radius * cyberspace.ExpandProgress;
            if (!IsValidCenter(center) || !float.IsFinite(radius) || radius <= 0f) {
                return -1;
            }
            float radiusSquared = radius * radius;
            int bestIndex = -1;
            float bestDistanceSquared = float.MaxValue;

            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!IsHostileTarget(npc) || IsBanishing(i)
                    || CyberBossExecution.IsExecuting(i)
                    || Vector2.DistanceSquared(npc.Center, center) > radiusSquared) {
                    continue;
                }
                Rectangle hitbox = npc.Hitbox;
                hitbox.Inflate(8, 8);
                if (!hitbox.Contains(mouse.ToPoint())) {
                    continue;
                }
                float distanceSquared = Vector2.DistanceSquared(npc.Center, mouse);
                if (distanceSquared < bestDistanceSquared) {
                    bestDistanceSquared = distanceSquared;
                    bestIndex = i;
                }
            }
            return bestIndex;
        }

        private static void ExecuteAuthoritativeBanish(Player owner,
            RamRequestToken request, NetworkNPCIdentity requestedIdentity,
            int responseClient) {
            BanishResultCode validation = ValidateAuthoritativeRequest(owner,
                requestedIdentity, out NPC requestedTarget,
                out NPC primaryTarget, out bool isBoss);
            if (validation != BanishResultCode.Success) {
                CompleteRamRequest(owner, request, validation, 0f, responseClient);
                return;
            }

            List<BanishTargetRecord> targets = CollectAuthoritativeTargets(
                requestedTarget, primaryTarget);
            if (targets.Count == 0
                || !ContainsIdentity(targets,
                    NetworkNPCIdentity.Capture(primaryTarget))) {
                CompleteRamRequest(owner, request, BanishResultCode.InvalidTarget,
                    0f, responseClient);
                return;
            }

            int cost = isBoss ? CyberBossExecution.RamCostPerCast : RamCostPerCast;
            float paid = 0f;
            if (!HackTime.InfiniteHackAuthority
                && !RamSystem.TryConsume(owner, cost, out paid)) {
                CompleteRamRequest(owner, request,
                    BanishResultCode.InsufficientRam, 0f, responseClient);
                if (Main.netMode == NetmodeID.SinglePlayer) {
                    PlayRequestFailure(owner);
                }
                return;
            }

            long activationId = AllocateActivationId();
            NetworkNPCIdentity primaryIdentity = NetworkNPCIdentity.Capture(primaryTarget);
            if (!TryApplyAuthoritativeActivation(owner.whoAmI, activationId,
                isBoss, primaryIdentity, targets, request,
                out BanishActivation activation)) {
                if (paid > 0f) {
                    RamSystem.Restore(owner, paid, out _);
                }
                CompleteRamRequest(owner, request, BanishResultCode.TargetBusy,
                    0f, responseClient);
                return;
            }

            if (Main.netMode == NetmodeID.Server) {
                SendApply(activation);
            }
            CompleteRamRequest(owner, request, BanishResultCode.Success,
                paid, responseClient);
        }

        private static BanishResultCode ValidateAuthoritativeRequest(Player owner,
            NetworkNPCIdentity requestedIdentity, out NPC requestedTarget,
            out NPC primaryTarget, out bool isBoss) {
            requestedTarget = null;
            primaryTarget = null;
            isBoss = false;
            if (owner?.active != true || owner.dead) {
                return BanishResultCode.InvalidPlayer;
            }
            CyberspacePlayer cyberspace = Cyberspace.For(owner);
            if (cyberspace == null || !cyberspace.Active
                || cyberspace.Intensity < 0.5f || cyberspace.CurrentLayer < 2) {
                return BanishResultCode.InvalidState;
            }
            if (!requestedIdentity.TryResolve(out requestedTarget)
                || !IsHostileTarget(requestedTarget)) {
                return BanishResultCode.InvalidTarget;
            }

            Vector2 domainCenter = owner.Center;
            float radius = cyberspace.Radius * cyberspace.ExpandProgress;
            float radiusSquared = radius * radius;
            if (!IsValidCenter(domainCenter) || !float.IsFinite(radius)
                || radius <= 0f || !float.IsFinite(radiusSquared)
                || !IsValidCenter(requestedTarget.Center)
                || Vector2.DistanceSquared(requestedTarget.Center, domainCenter)
                    > radiusSquared) {
                return BanishResultCode.OutsideDomain;
            }
            if (IsBanishing(requestedIdentity)
                || CyberBossExecution.IsExecuting(requestedIdentity)) {
                return BanishResultCode.TargetBusy;
            }

            int primaryIndex = NpcGroupHelper.GetAnchorIndex(requestedTarget);
            primaryTarget = primaryIndex >= 0 && primaryIndex < Main.maxNPCs
                && Main.npc[primaryIndex].active
                ? Main.npc[primaryIndex]
                : requestedTarget;
            if (!NetworkNPCIdentity.TryCapture(primaryTarget, out _)) {
                return BanishResultCode.InvalidTarget;
            }
            isBoss = CyberBossExecution.IsBossTier(requestedTarget);
            return BanishResultCode.Success;
        }

        private static List<BanishTargetRecord> CollectAuthoritativeTargets(
            NPC requestedTarget, NPC primaryTarget) {
            List<BanishTargetRecord> targets = [];
            HashSet<NetworkNPCIdentity> identities = [];
            NpcGroupHelper.CollectGroup(requestedTarget, groupBuffer);
            AddAuthoritativeTarget(primaryTarget, true, identities, targets);
            for (int i = 0; i < groupBuffer.Count; i++) {
                NPC member = groupBuffer[i];
                AddAuthoritativeTarget(member, member == primaryTarget,
                    identities, targets);
            }
            groupBuffer.Clear();
            return targets;
        }

        private static void AddAuthoritativeTarget(NPC npc, bool isPrimary,
            HashSet<NetworkNPCIdentity> identities,
            List<BanishTargetRecord> targets) {
            if (npc?.active != true || !IsValidCenter(npc.Center)
                || IsBanishing(npc.whoAmI)
                || CyberBossExecution.IsExecuting(npc.whoAmI)
                || !NetworkNPCIdentity.TryCapture(npc,
                    out NetworkNPCIdentity identity)
                || !identities.Add(identity)) {
                return;
            }
            Vector2 resumeVelocity = SanitizeVelocity(
                TimeFreezeSystem.GetEffectiveResumeVelocity(npc));
            targets.Add(new BanishTargetRecord(identity, Main.rand.NextFloat(),
                npc.Center, resumeVelocity, isPrimary));
        }

        private static bool TryApplyAuthoritativeActivation(int ownerWho,
            long activationId, bool isBoss, NetworkNPCIdentity primaryIdentity,
            List<BanishTargetRecord> targets, RamRequestToken request,
            out BanishActivation activation) {
            activation = null;
            if (!IsValidActivation(ownerWho, activationId, 0,
                BanishDuration, isBoss, primaryIdentity, targets)) {
                return false;
            }

            activation = new BanishActivation(ownerWho, activationId, isBoss,
                primaryIdentity, 0, false, true, request, targets);
            activeActivations.Add(activation);
            List<BanishTargetRecord> accepted = [];
            for (int i = 0; i < targets.Count; i++) {
                BanishTargetRecord target = targets[i];
                if (target.Identity.TryResolve(out NPC npc)
                    && TryAttachTarget(activation, target, npc,
                        replaceConflicts: false)) {
                    accepted.Add(target);
                }
            }
            activation.Targets.Clear();
            activation.Targets.AddRange(accepted);
            if (!ContainsIdentity(accepted, primaryIdentity)) {
                EndActivation(activation, broadcast: false, spawnBurst: false);
                activation = null;
                return false;
            }
            return true;
        }

        private static bool ApplyReplicatedActivation(int ownerWho,
            long activationId, bool isBoss, NetworkNPCIdentity primaryIdentity,
            int elapsed, bool executionTriggered,
            List<BanishTargetRecord> targets) {
            if (WasReleased(activationId)
                || !IsValidActivation(ownerWho, activationId, elapsed,
                    BanishDuration, isBoss, primaryIdentity, targets)) {
                return false;
            }

            BanishActivation activation = FindActivation(activationId);
            if (activation == null) {
                activation = new BanishActivation(ownerWho, activationId, isBoss,
                    primaryIdentity, elapsed, executionTriggered, false,
                    default, []);
                activeActivations.Add(activation);
            }
            else if (activation.OwnerWho != ownerWho
                || activation.IsBoss != isBoss
                || activation.PrimaryIdentity != primaryIdentity) {
                return false;
            }
            activation.Timer = Math.Max(activation.Timer, elapsed);
            activation.ExecutionTriggered |= executionTriggered;

            for (int i = 0; i < targets.Count; i++) {
                BanishTargetRecord target = targets[i];
                if (!TryMergeTarget(activation, target)) {
                    continue;
                }
                PrepareIncomingIdentity(activationId, target.Identity);
                int remaining = BanishDuration - activation.Timer;
                TimeControlReplicationSystem.ResolveOrQueueNPC<CyberBanish>(
                    activationId, target.Identity, remaining,
                    npc => TryAttachPendingTarget(activationId, target, npc));
            }
            return activation.Targets.Count > 0;
        }

        private static bool TryMergeTarget(BanishActivation activation,
            BanishTargetRecord target) {
            for (int i = 0; i < activation.Targets.Count; i++) {
                BanishTargetRecord existing = activation.Targets[i];
                if (existing.Identity.Index == target.Identity.Index
                    && existing.Identity != target.Identity) {
                    return false;
                }
                if (existing.Identity != target.Identity) {
                    continue;
                }
                return existing == target;
            }
            activation.Targets.Add(target);
            return true;
        }

        private static void TryAttachPendingTarget(long activationId,
            BanishTargetRecord target, NPC npc) {
            BanishActivation activation = FindActivation(activationId);
            if (activation == null || WasReleased(activationId)
                || !activation.Targets.Contains(target)) {
                return;
            }
            TryAttachTarget(activation, target, npc, replaceConflicts: true);
        }

        private static bool TryAttachTarget(BanishActivation activation,
            BanishTargetRecord target, NPC npc, bool replaceConflicts) {
            if (activation == null || npc?.active != true
                || !target.Identity.TryResolve(out NPC resolved)
                || resolved != npc) {
                return false;
            }
            for (int i = 0; i < activation.Entries.Count; i++) {
                if (activation.Entries[i].Identity == target.Identity) {
                    return true;
                }
            }
            if (replaceConflicts) {
                RemoveConflictingEntries(activation.ActivationId,
                    target.Identity);
            }
            else if (IsBanishing(npc.whoAmI)) {
                return false;
            }

            TimeFreezeLease lease = TimeFreezeSystem.AcquireNPC<CyberBanish>(
                npc, target.Center, activation.ActivationId,
                TimeFreezeAnchorPriority.Authoritative);
            if (!lease.IsValid) {
                return false;
            }

            BanishEntry entry = new() {
                NpcIndex = npc.whoAmI,
                OriginalScale = npc.scale,
                Seed = target.Seed,
                ActivationId = activation.ActivationId,
                Identity = target.Identity,
                FreezeCenter = target.Center,
                ResumeVelocity = target.ResumeVelocity,
                FreezeLease = lease,
                Activation = activation,
            };
            activation.Entries.Add(entry);
            ActiveBanishments.Add(entry);
            if (!Main.dedServ) {
                SoundEngine.PlaySound(CWRSound.Fault, npc.Center);
            }
            return true;
        }

        public static void Update() {
            PruneReleasedActivations();
            if (Main.netMode == NetmodeID.MultiplayerClient) {
                UpdateClientPresentation();
                return;
            }
            UpdateAuthoritativeActivations();
        }

        private static void UpdateAuthoritativeActivations() {
            for (int i = activeActivations.Count - 1; i >= 0; i--) {
                BanishActivation activation = activeActivations[i];
                if (!IsValidOwner(activation.OwnerWho)
                    || Main.player[activation.OwnerWho]?.active != true
                    || Main.player[activation.OwnerWho].dead) {
                    EndActivation(activation,
                        broadcast: Main.netMode == NetmodeID.Server,
                        spawnBurst: false);
                    continue;
                }

                PruneInvalidTargets(activation);
                if (activation.Targets.Count == 0
                    || activation.IsBoss
                    && !activation.PrimaryIdentity.TryResolve(out _)) {
                    EndActivation(activation,
                        broadcast: Main.netMode == NetmodeID.Server,
                        spawnBurst: false);
                    continue;
                }

                activation.Timer = Math.Min(BanishDuration,
                    activation.Timer
                    + TimeGear.PullFrameAdvance(ref activation.TimerCarry));
                SpawnPresentationParticles(activation);

                if (activation.IsBoss && !activation.ExecutionTriggered
                    && activation.Progress >= BossExecutionThreshold
                    && activation.PrimaryIdentity.TryResolve(out NPC boss)) {
                    activation.ExecutionTriggered = true;
                    CyberBossExecution.StartExecution(activation.ActivationId,
                        activation.PrimaryIdentity,
                        Main.player[activation.OwnerWho]);
                }

                if (activation.Timer >= BanishDuration) {
                    CompleteActivation(activation);
                }
            }
        }

        private static void UpdateClientPresentation() {
            for (int i = 0; i < activeActivations.Count; i++) {
                BanishActivation activation = activeActivations[i];
                activation.Timer = Math.Min(BanishDuration - 1,
                    activation.Timer
                    + TimeGear.PullFrameAdvance(ref activation.TimerCarry));
                SpawnPresentationParticles(activation);
            }
        }

        private static void SpawnPresentationParticles(BanishActivation activation) {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < activation.Entries.Count; i++) {
                BanishEntry entry = activation.Entries[i];
                if (entry.Identity.TryResolve(out NPC npc)
                    && TimeFreezeSystem.IsLeaseActive(npc, entry.FreezeLease)) {
                    CyberBanishParticles.SpawnBanishParticles(npc,
                        activation.Progress, entry.Seed);
                }
            }
        }

        private static void CompleteActivation(BanishActivation activation) {
            List<NPC> removedTargets = [];
            if (!activation.IsBoss) {
                for (int i = 0; i < activation.Targets.Count; i++) {
                    if (activation.Targets[i].Identity.TryResolve(out NPC npc)) {
                        removedTargets.Add(npc);
                    }
                }
            }

            EndActivation(activation,
                broadcast: Main.netMode == NetmodeID.Server,
                spawnBurst: !activation.IsBoss);
            for (int i = 0; i < removedTargets.Count; i++) {
                NPC npc = removedTargets[i];
                if (!npc.active) {
                    continue;
                }
                npc.life = 0;
                npc.active = false;
                if (Main.netMode == NetmodeID.Server) {
                    NetMessage.SendData(MessageID.SyncNPC, -1, -1, null,
                        npc.whoAmI);
                }
            }
        }

        private static void EndActivation(BanishActivation activation,
            bool broadcast, bool spawnBurst) {
            if (activation == null) {
                return;
            }
            if (broadcast) {
                SendRelease(activation.ActivationId);
            }
            for (int i = activation.Targets.Count - 1; i >= 0; i--) {
                TimeControlReplicationSystem.CancelNPC<CyberBanish>(
                    activation.ActivationId, activation.Targets[i].Identity);
            }
            for (int i = activation.Entries.Count - 1; i >= 0; i--) {
                ReleaseEntry(activation.Entries[i], spawnBurst);
            }
            activeActivations.Remove(activation);
            RememberReleased(activation.ActivationId);
        }

        private static void ReleaseEntry(BanishEntry entry, bool spawnBurst) {
            if (entry == null) {
                return;
            }
            NPC npc = null;
            if (entry.Identity.TryResolve(out NPC resolved)) {
                npc = resolved;
                TimeFreezeSystem.ReleaseNPC(npc, entry.FreezeLease,
                    entry.ResumeVelocity);
            }
            if (spawnBurst && !Main.dedServ) {
                CyberBanishParticles.SpawnFinalBurst(
                    npc?.Center ?? entry.FreezeCenter, entry.OriginalScale);
            }
            entry.Activation?.Entries.Remove(entry);
            ActiveBanishments.Remove(entry);
        }

        private static void PruneInvalidTargets(BanishActivation activation) {
            for (int i = activation.Targets.Count - 1; i >= 0; i--) {
                BanishTargetRecord target = activation.Targets[i];
                if (target.Identity.TryResolve(out _)
                    && HasActiveEntry(activation, target.Identity)) {
                    continue;
                }
                TimeControlReplicationSystem.CancelNPC<CyberBanish>(
                    activation.ActivationId, target.Identity);
                for (int j = activation.Entries.Count - 1; j >= 0; j--) {
                    if (activation.Entries[j].Identity == target.Identity) {
                        ReleaseEntry(activation.Entries[j], spawnBurst: false);
                    }
                }
                activation.Targets.RemoveAt(i);
            }
        }

        private static bool HasActiveEntry(BanishActivation activation,
            NetworkNPCIdentity identity) {
            for (int i = 0; i < activation.Entries.Count; i++) {
                BanishEntry entry = activation.Entries[i];
                if (entry.Identity == identity && IsEntryActive(entry)) {
                    return true;
                }
            }
            return false;
        }

        private static void PrepareIncomingIdentity(long activationId,
            NetworkNPCIdentity identity) {
            RemoveConflictingEntries(activationId, identity);
            for (int i = activeActivations.Count - 1; i >= 0; i--) {
                BanishActivation other = activeActivations[i];
                if (other.ActivationId == activationId) {
                    continue;
                }
                for (int j = other.Targets.Count - 1; j >= 0; j--) {
                    BanishTargetRecord target = other.Targets[j];
                    if (target.Identity.Index != identity.Index) {
                        continue;
                    }
                    TimeControlReplicationSystem.CancelNPC<CyberBanish>(
                        other.ActivationId, target.Identity);
                    other.Targets.RemoveAt(j);
                }
                if (other.Targets.Count == 0) {
                    activeActivations.RemoveAt(i);
                    RememberReleased(other.ActivationId);
                }
            }
        }

        private static void RemoveConflictingEntries(long activationId,
            NetworkNPCIdentity identity) {
            for (int i = ActiveBanishments.Count - 1; i >= 0; i--) {
                BanishEntry entry = ActiveBanishments[i];
                if (entry.NpcIndex == identity.Index
                    && (entry.ActivationId != activationId
                        || entry.Identity != identity)) {
                    ReleaseEntry(entry, spawnBurst: false);
                }
            }
        }

        private static BanishActivation FindActivation(long activationId) {
            for (int i = 0; i < activeActivations.Count; i++) {
                if (activeActivations[i].ActivationId == activationId) {
                    return activeActivations[i];
                }
            }
            return null;
        }

        private static bool ContainsIdentity(
            IReadOnlyList<BanishTargetRecord> targets,
            NetworkNPCIdentity identity) {
            for (int i = 0; i < targets.Count; i++) {
                if (targets[i].Identity == identity) {
                    return true;
                }
            }
            return false;
        }

        private static bool IsEntryActive(BanishEntry entry)
            => entry != null && entry.Identity.TryResolve(out NPC npc)
            && npc.whoAmI == entry.NpcIndex
            && TimeFreezeSystem.IsLeaseActive(npc, entry.FreezeLease);

        private static bool IsHostileTarget(NPC npc)
            => npc?.active == true && !npc.friendly && !npc.townNPC
            && npc.lifeMax > 0;

        private static long AllocateActivationId() {
            nextActivationId = nextActivationId >= long.MaxValue
                ? 1 : nextActivationId + 1;
            return nextActivationId;
        }

        private static Vector2 SanitizeVelocity(Vector2 velocity) {
            if (!float.IsFinite(velocity.X) || !float.IsFinite(velocity.Y)) {
                return Vector2.Zero;
            }
            const float maxComponent = 4096f;
            return new Vector2(
                MathHelper.Clamp(velocity.X, -maxComponent, maxComponent),
                MathHelper.Clamp(velocity.Y, -maxComponent, maxComponent));
        }

        private static void PlayRequestFailure(Player owner) {
            if (Main.dedServ || owner?.active != true
                || owner.whoAmI != Main.myPlayer) {
                return;
            }
            SoundEngine.PlaySound(CWRSound.FailureCurrent with {
                Volume = 0.4f,
                Pitch = -0.3f,
            }, owner.Center);
            RamSystem.NotifyInsufficient();
            CombatText.NewText(owner.Hitbox, new Color(255, 90, 80),
                "// LOW RAM", true);
        }

        public static void Reset() {
            for (int i = activeActivations.Count - 1; i >= 0; i--) {
                EndActivation(activeActivations[i], broadcast: false,
                    spawnBurst: false);
            }
            activeActivations.Clear();
            ActiveBanishments.Clear();
            TimeControlReplicationSystem.CancelAll<CyberBanish>();
            ClearReleasedActivations();
            groupBuffer.Clear();
        }
    }

    internal readonly record struct BanishTargetRecord(
        NetworkNPCIdentity Identity,
        float Seed,
        Vector2 Center,
        Vector2 ResumeVelocity,
        bool IsPrimary);

    internal sealed class BanishActivation
    {
        internal int OwnerWho;
        internal long ActivationId;
        internal bool IsBoss;
        internal NetworkNPCIdentity PrimaryIdentity;
        internal int Timer;
        internal float TimerCarry;
        internal bool ExecutionTriggered;
        internal bool Authoritative;
        internal RamRequestToken Request;
        internal readonly List<BanishTargetRecord> Targets;
        internal readonly List<BanishEntry> Entries = [];

        internal float Progress => MathHelper.Clamp(
            Timer / (float)CyberBanish.BanishDuration, 0f, 1f);

        internal BanishActivation(int ownerWho, long activationId, bool isBoss,
            NetworkNPCIdentity primaryIdentity, int timer,
            bool executionTriggered, bool authoritative,
            RamRequestToken request, List<BanishTargetRecord> targets) {
            OwnerWho = ownerWho;
            ActivationId = activationId;
            IsBoss = isBoss;
            PrimaryIdentity = primaryIdentity;
            Timer = timer;
            ExecutionTriggered = executionTriggered;
            Authoritative = authoritative;
            Request = request;
            Targets = targets;
        }
    }

    internal sealed class BanishEntry
    {
        public int NpcIndex;
        public float OriginalScale;
        public float Seed;
        internal long ActivationId;
        internal NetworkNPCIdentity Identity;
        internal Vector2 FreezeCenter;
        internal Vector2 ResumeVelocity;
        internal TimeFreezeLease FreezeLease;
        internal BanishActivation Activation;

        public int Timer => Activation?.Timer ?? 0;
        public bool IsBoss => Activation?.IsBoss == true;
        public int OwnerWho => Activation?.OwnerWho ?? -1;
        public bool ExecutionTriggered => Activation?.ExecutionTriggered == true;
        public float Progress => Activation?.Progress ?? 0f;
    }
}

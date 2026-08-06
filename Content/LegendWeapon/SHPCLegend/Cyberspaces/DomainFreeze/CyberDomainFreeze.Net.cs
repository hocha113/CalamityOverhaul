using CalamityOverhaul.Content.RAMSystems;
using CalamityOverhaul.Content.TimeFreezes;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces.DomainFreeze
{
    internal partial class CyberDomainFreeze
    {
        private enum FreezePacketKind : byte
        {
            Request,
            Apply,
            ReleaseNPC,
            ReleaseProjectile,
            AdvanceNPC,
        }

        private enum FreezeResultCode : byte
        {
            Success,
            InvalidRequest,
            InvalidState,
            InsufficientRam,
            ConflictingRequest,
            ExpiredRequest,
        }

        private readonly record struct NPCSnapshotRecord(int OwnerWho,
            long ActivationId, int Elapsed, int Duration, NPCFreezeTarget Target);

        private readonly record struct ProjectileSnapshotRecord(int OwnerWho,
            long ActivationId, int Elapsed, int Duration,
            ProjectileFreezeTarget Target);

        private readonly record struct NPCReleaseRecord(long ActivationId,
            NetworkNPCIdentity Identity);

        private readonly record struct ProjectileReleaseRecord(long ActivationId,
            NetworkProjectileIdentity Identity);

        private readonly record struct NPCAdvanceRecord(long ActivationId,
            NetworkNPCIdentity Identity, int Elapsed, int Duration);

        private const byte SnapshotVersion = 1;
        private const int MaxRememberedActivations = 256;
        private const int ReleasedRetentionFrames = 120;
        private const float WorldCoordinateMargin = 8192f;

        private static readonly HashSet<long> rememberedActivations = [];
        private static readonly Queue<long> activationOrder = [];
        private static readonly Dictionary<NPCReleaseRecord, ulong> releasedNPCs = [];
        private static readonly Dictionary<ProjectileReleaseRecord, ulong>
            releasedProjectiles = [];

        //解冻/推进广播按帧攒批：线格式本来就带 count，一次领域解冻不该发几百个包。
        //客户端遇到重复记录会整包丢弃，所以入队时必须去重。
        private static readonly List<NPCReleaseRecord> pendingNPCReleases = [];
        private static readonly HashSet<NPCReleaseRecord> pendingNPCReleaseKeys = [];
        private static readonly List<ProjectileReleaseRecord>
            pendingProjectileReleases = [];
        private static readonly HashSet<ProjectileReleaseRecord>
            pendingProjectileReleaseKeys = [];
        private static readonly List<NPCAdvanceRecord> pendingNPCAdvances = [];
        private static readonly HashSet<NPCReleaseRecord> pendingNPCAdvanceKeys = [];

        private static void SendFreezeRequest(RamRequestToken request) {
            if (Main.netMode != NetmodeID.MultiplayerClient || !request.IsValid) {
                return;
            }
            ModPacket packet = CWRMod.Instance.GetPacket();
            packet.Write((byte)CWRMessageType.CyberDomainFreezeStart);
            packet.Write((byte)FreezePacketKind.Request);
            packet.Write(request.SessionId);
            packet.Write(request.RequestId);
            packet.Send();
        }

        internal static void HandleNetStart(BinaryReader reader, int whoAmI) {
            if (reader == null) {
                return;
            }
            try {
                FreezePacketKind kind = (FreezePacketKind)reader.ReadByte();
                if (Main.netMode == NetmodeID.Server) {
                    if (kind == FreezePacketKind.Request) {
                        HandleFreezeRequest(reader, whoAmI);
                    }
                    return;
                }
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    return;
                }

                switch (kind) {
                    case FreezePacketKind.Apply:
                        HandleApply(reader);
                        break;
                    case FreezePacketKind.ReleaseNPC:
                        HandleReleaseNPC(reader);
                        break;
                    case FreezePacketKind.ReleaseProjectile:
                        HandleReleaseProjectile(reader);
                        break;
                    case FreezePacketKind.AdvanceNPC:
                        HandleAdvanceNPC(reader);
                        break;
                }
            } catch (EndOfStreamException) {
            } catch (IOException) {
            } catch (ObjectDisposedException) {
            }
        }

        private static void HandleFreezeRequest(BinaryReader reader, int whoAmI) {
            uint sessionId = reader.ReadUInt32();
            uint requestId = reader.ReadUInt32();
            if (whoAmI < 0 || whoAmI >= Main.maxPlayers) {
                return;
            }
            Player owner = Main.player[whoAmI];
            if (owner?.active != true || requestId == 0) {
                RamNet.SendStateSnapshot(owner, whoAmI);
                return;
            }

            RamRequestDisposition disposition = RamSystem.ClassifyRequest(owner,
                sessionId, requestId, RamOperationId, out RamRequestResult previous);
            if (disposition == RamRequestDisposition.Replay) {
                RamNet.SendRequestResult(owner, previous, whoAmI);
                return;
            }
            if (disposition == RamRequestDisposition.Invalid) {
                RamNet.SendStateSnapshot(owner, whoAmI);
                return;
            }
            if (disposition != RamRequestDisposition.New) {
                FreezeResultCode code = disposition == RamRequestDisposition.Conflict
                    ? FreezeResultCode.ConflictingRequest
                    : FreezeResultCode.ExpiredRequest;
                RamNet.SendRejectedRequest(owner, sessionId, requestId,
                    RamOperationId, (byte)code, whoAmI);
                return;
            }

            ExecuteAuthoritativeFreeze(owner,
                new RamRequestToken(sessionId, requestId), whoAmI);
        }

        private static void BroadcastApply(int ownerWho, long activationId,
            IReadOnlyList<NPCFreezeTarget> npcTargets,
            IReadOnlyList<ProjectileFreezeTarget> projectileTargets,
            int elapsed, int duration) {
            if (Main.netMode != NetmodeID.Server || !IsValidOwner(ownerWho)
                || activationId <= 0 || !IsValidTiming(elapsed, duration)
                || npcTargets == null || projectileTargets == null
                || npcTargets.Count > Main.maxNPCs
                || projectileTargets.Count > Main.maxProjectiles
                || npcTargets.Count + projectileTargets.Count
                    > Main.maxNPCs + Main.maxProjectiles) {
                return;
            }
            HashSet<NetworkNPCIdentity> npcIdentities = [];
            HashSet<int> npcIndices = [];
            for (int i = 0; i < npcTargets.Count; i++) {
                NPCFreezeTarget target = npcTargets[i];
                if (!IsValidTarget(target)
                    || !npcIdentities.Add(target.Identity)
                    || !npcIndices.Add(target.Identity.Index)) {
                    return;
                }
            }
            HashSet<NetworkProjectileIdentity> projectileIdentities = [];
            for (int i = 0; i < projectileTargets.Count; i++) {
                ProjectileFreezeTarget target = projectileTargets[i];
                if (!IsValidTarget(target)
                    || !projectileIdentities.Add(target.Identity)) {
                    return;
                }
            }

            ModPacket packet = NewPacket(FreezePacketKind.Apply);
            packet.Write((byte)ownerWho);
            packet.Write(activationId);
            packet.Write((ushort)elapsed);
            packet.Write((ushort)duration);
            packet.Write((ushort)npcTargets.Count);
            for (int i = 0; i < npcTargets.Count; i++) {
                WriteTarget(packet, npcTargets[i]);
            }
            packet.Write((ushort)projectileTargets.Count);
            for (int i = 0; i < projectileTargets.Count; i++) {
                WriteTarget(packet, projectileTargets[i]);
            }
            packet.Send();
        }

        private static void BroadcastReleaseNPC(long activationId,
            NetworkNPCIdentity identity) {
            if (Main.netMode != NetmodeID.Server || activationId <= 0
                || !identity.IsValid) {
                return;
            }
            NPCReleaseRecord record = new(activationId, identity);
            if (pendingNPCReleaseKeys.Add(record)) {
                pendingNPCReleases.Add(record);
            }
        }

        private static void BroadcastReleaseProjectile(long activationId,
            NetworkProjectileIdentity identity) {
            if (Main.netMode != NetmodeID.Server || activationId <= 0
                || !identity.IsValid) {
                return;
            }
            ProjectileReleaseRecord record = new(activationId, identity);
            if (pendingProjectileReleaseKeys.Add(record)) {
                pendingProjectileReleases.Add(record);
            }
        }

        private static void BroadcastAdvanceNPC(FreezeEntry entry) {
            if (Main.netMode != NetmodeID.Server || entry == null
                || entry.ActivationId <= 0 || !entry.Identity.IsValid
                || !IsValidTiming(entry.Timer, entry.Duration)) {
                return;
            }
            if (pendingNPCAdvanceKeys.Add(new NPCReleaseRecord(entry.ActivationId,
                entry.Identity))) {
                pendingNPCAdvances.Add(new NPCAdvanceRecord(entry.ActivationId,
                    entry.Identity, entry.Timer, entry.Duration));
            }
        }

        /// <summary>把本帧攒下的解冻/推进记录各发一包</summary>
        private static void FlushBroadcasts() {
            if (Main.netMode != NetmodeID.Server) {
                ClearPendingBroadcasts();
                return;
            }
            //分片上限对齐接收端的 count 校验，超限的批次整包会被丢弃
            for (int start = 0; start < pendingNPCReleases.Count;
                start += Main.maxNPCs) {
                int count = Math.Min(Main.maxNPCs,
                    pendingNPCReleases.Count - start);
                ModPacket packet = NewPacket(FreezePacketKind.ReleaseNPC);
                packet.Write((ushort)count);
                for (int i = start; i < start + count; i++) {
                    NPCReleaseRecord record = pendingNPCReleases[i];
                    packet.Write(record.ActivationId);
                    record.Identity.Write(packet);
                }
                packet.Send();
            }
            for (int start = 0; start < pendingProjectileReleases.Count;
                start += Main.maxProjectiles) {
                int count = Math.Min(Main.maxProjectiles,
                    pendingProjectileReleases.Count - start);
                ModPacket packet = NewPacket(FreezePacketKind.ReleaseProjectile);
                packet.Write((ushort)count);
                for (int i = start; i < start + count; i++) {
                    ProjectileReleaseRecord record = pendingProjectileReleases[i];
                    packet.Write(record.ActivationId);
                    record.Identity.Write(packet);
                }
                packet.Send();
            }
            for (int start = 0; start < pendingNPCAdvances.Count;
                start += Main.maxNPCs) {
                int count = Math.Min(Main.maxNPCs,
                    pendingNPCAdvances.Count - start);
                ModPacket packet = NewPacket(FreezePacketKind.AdvanceNPC);
                packet.Write((ushort)count);
                for (int i = start; i < start + count; i++) {
                    NPCAdvanceRecord record = pendingNPCAdvances[i];
                    packet.Write(record.ActivationId);
                    record.Identity.Write(packet);
                    packet.Write((ushort)record.Elapsed);
                    packet.Write((ushort)record.Duration);
                }
                packet.Send();
            }
            ClearPendingBroadcasts();
        }

        private static void ClearPendingBroadcasts() {
            pendingNPCReleases.Clear();
            pendingNPCReleaseKeys.Clear();
            pendingProjectileReleases.Clear();
            pendingProjectileReleaseKeys.Clear();
            pendingNPCAdvances.Clear();
            pendingNPCAdvanceKeys.Clear();
        }

        private static void HandleApply(BinaryReader reader) {
            if (!TryReadApply(reader, out int ownerWho, out long activationId,
                out int elapsed, out int duration,
                out List<NPCFreezeTarget> npcTargets,
                out List<ProjectileFreezeTarget> projectileTargets)) {
                return;
            }

            bool firstApply = RememberActivation(activationId);
            ApplyFreezeBatch(ownerWho, activationId, npcTargets,
                projectileTargets, replicated: true, elapsed, duration,
                out List<NPCFreezeTarget> acceptedNPCs,
                out List<ProjectileFreezeTarget> acceptedProjectiles);
            bool emptyActivation = npcTargets.Count == 0
                && projectileTargets.Count == 0;
            if (firstApply && (emptyActivation || acceptedNPCs.Count > 0
                || acceptedProjectiles.Count > 0)) {
                PlayActivationWave(Main.player[ownerWho]);
            }
        }

        private static void HandleReleaseNPC(BinaryReader reader) {
            int count = reader.ReadUInt16();
            if (count <= 0 || count > Main.maxNPCs) {
                return;
            }
            List<NPCReleaseRecord> records = new(count);
            HashSet<NPCReleaseRecord> identities = [];
            for (int i = 0; i < count; i++) {
                long activationId = reader.ReadInt64();
                if (activationId <= 0
                    || !NetworkNPCIdentity.TryRead(reader,
                        out NetworkNPCIdentity identity)) {
                    return;
                }
                NPCReleaseRecord record = new(activationId, identity);
                if (!identities.Add(record)) {
                    return;
                }
                records.Add(record);
            }
            for (int i = 0; i < records.Count; i++) {
                NPCReleaseRecord record = records[i];
                ApplyReleaseNPC(record.ActivationId, record.Identity,
                    spawnBurst: true);
            }
        }

        private static void HandleReleaseProjectile(BinaryReader reader) {
            int count = reader.ReadUInt16();
            if (count <= 0 || count > Main.maxProjectiles) {
                return;
            }
            List<ProjectileReleaseRecord> records = new(count);
            HashSet<ProjectileReleaseRecord> identities = [];
            for (int i = 0; i < count; i++) {
                long activationId = reader.ReadInt64();
                if (activationId <= 0
                    || !NetworkProjectileIdentity.TryRead(reader,
                        out NetworkProjectileIdentity identity)) {
                    return;
                }
                ProjectileReleaseRecord record = new(activationId, identity);
                if (!identities.Add(record)) {
                    return;
                }
                records.Add(record);
            }
            for (int i = 0; i < records.Count; i++) {
                ProjectileReleaseRecord record = records[i];
                ApplyReleaseProjectile(record.ActivationId, record.Identity);
            }
        }

        private static void HandleAdvanceNPC(BinaryReader reader) {
            int count = reader.ReadUInt16();
            if (count <= 0 || count > Main.maxNPCs) {
                return;
            }
            List<NPCAdvanceRecord> records = new(count);
            HashSet<NPCReleaseRecord> identities = [];
            for (int i = 0; i < count; i++) {
                long activationId = reader.ReadInt64();
                if (activationId <= 0
                    || !NetworkNPCIdentity.TryRead(reader,
                        out NetworkNPCIdentity identity)) {
                    return;
                }
                int elapsed = reader.ReadUInt16();
                int duration = reader.ReadUInt16();
                if (!IsValidTiming(elapsed, duration)
                    || !identities.Add(new NPCReleaseRecord(activationId,
                        identity))) {
                    return;
                }
                records.Add(new NPCAdvanceRecord(activationId, identity,
                    elapsed, duration));
            }
            for (int i = 0; i < records.Count; i++) {
                NPCAdvanceRecord record = records[i];
                ApplyAdvanceNPC(record.ActivationId, record.Identity,
                    record.Elapsed, record.Duration);
            }
        }

        private static bool TryReadApply(BinaryReader reader, out int ownerWho,
            out long activationId, out int elapsed, out int duration,
            out List<NPCFreezeTarget> npcTargets,
            out List<ProjectileFreezeTarget> projectileTargets) {
            ownerWho = reader.ReadByte();
            activationId = reader.ReadInt64();
            elapsed = reader.ReadUInt16();
            duration = reader.ReadUInt16();
            npcTargets = [];
            projectileTargets = [];
            if (!IsValidOwner(ownerWho) || activationId <= 0
                || !IsValidTiming(elapsed, duration)) {
                return false;
            }

            int npcCount = reader.ReadUInt16();
            if (npcCount > Main.maxNPCs) {
                return false;
            }
            HashSet<NetworkNPCIdentity> npcIdentities = [];
            HashSet<int> npcIndices = [];
            npcTargets = new List<NPCFreezeTarget>(npcCount);
            for (int i = 0; i < npcCount; i++) {
                if (!TryReadNPCTarget(reader, out NPCFreezeTarget target)
                    || !npcIdentities.Add(target.Identity)
                    || !npcIndices.Add(target.Identity.Index)) {
                    return false;
                }
                npcTargets.Add(target);
            }

            int projectileCount = reader.ReadUInt16();
            if (projectileCount > Main.maxProjectiles
                || npcCount + projectileCount
                    > Main.maxNPCs + Main.maxProjectiles) {
                return false;
            }
            HashSet<NetworkProjectileIdentity> projectileIdentities = [];
            projectileTargets = new List<ProjectileFreezeTarget>(projectileCount);
            for (int i = 0; i < projectileCount; i++) {
                if (!TryReadProjectileTarget(reader,
                    out ProjectileFreezeTarget target)
                    || !projectileIdentities.Add(target.Identity)) {
                    return false;
                }
                projectileTargets.Add(target);
            }
            return true;
        }

        private static void ApplyReleaseNPC(long activationId,
            NetworkNPCIdentity identity, bool spawnBurst) {
            RememberReleasedNPC(activationId, identity);
            TimeControlReplicationSystem.CancelNPC<CyberDomainFreeze>(
                activationId, identity);
            for (int i = FrozenNPCs.Count - 1; i >= 0; i--) {
                FreezeEntry entry = FrozenNPCs[i];
                if (entry.ActivationId == activationId
                    && entry.Identity == identity) {
                    RemoveNPCEntryAt(i, spawnBurst, broadcast: false);
                }
            }
        }

        private static void ApplyReleaseProjectile(long activationId,
            NetworkProjectileIdentity identity) {
            RememberReleasedProjectile(activationId, identity);
            TimeControlReplicationSystem.CancelProjectile<CyberDomainFreeze>(
                activationId, identity);
            for (int i = FrozenProjectiles.Count - 1; i >= 0; i--) {
                FreezeProjEntry entry = FrozenProjectiles[i];
                if (entry.ActivationId == activationId
                    && entry.Identity == identity) {
                    RemoveProjectileEntryAt(i, broadcast: false);
                }
            }
        }

        private static void ApplyAdvanceNPC(long activationId,
            NetworkNPCIdentity identity, int elapsed, int duration) {
            for (int i = 0; i < FrozenNPCs.Count; i++) {
                FreezeEntry entry = FrozenNPCs[i];
                if (entry.ActivationId != activationId
                    || entry.Identity != identity || entry.Duration != duration) {
                    continue;
                }
                int previous = entry.Timer;
                entry.Timer = Math.Max(entry.Timer, elapsed);
                int thawStart = Math.Max(0, duration - AcceleratedThawFrames);
                if (previous < thawStart && entry.Timer >= thawStart
                    && identity.TryResolve(out NPC npc)) {
                    PlayThawSound(npc);
                }
                return;
            }
        }

        private static bool QueueNPCApply(int ownerWho, long activationId,
            NPCFreezeTarget target, int elapsed, int duration) {
            for (int i = 0; i < FrozenNPCs.Count; i++) {
                FreezeEntry existing = FrozenNPCs[i];
                if (existing.ActivationId != activationId
                    || existing.Identity != target.Identity) {
                    continue;
                }
                if (existing.OwnerWho != ownerWho || existing.Duration != duration
                    || existing.Seed != target.Seed
                    || existing.FreezeCenter != target.Center) {
                    return false;
                }
                existing.Timer = Math.Max(existing.Timer, elapsed);
                if (existing.EntityIndex < 0) {
                    if (existing.ResolutionExpiresAt != 0
                        && Main.GameUpdateCount < existing.ResolutionExpiresAt) {
                        return true;
                    }
                    TimeControlReplicationSystem.CancelNPC<CyberDomainFreeze>(
                        activationId, target.Identity);
                    return QueueNPCResolution(existing,
                        duration - existing.Timer);
                }
                return true;
            }

            FreezeEntry pending = new() {
                EntityIndex = -1,
                Identity = target.Identity,
                ActivationId = activationId,
                OwnerWho = ownerWho,
                Timer = elapsed,
                Duration = duration,
                Seed = target.Seed,
                FreezeCenter = target.Center,
            };
            FrozenNPCs.Add(pending);
            return QueueNPCResolution(pending, duration - elapsed);
        }

        private static bool QueueProjectileApply(int ownerWho, long activationId,
            ProjectileFreezeTarget target, int elapsed, int duration) {
            for (int i = 0; i < FrozenProjectiles.Count; i++) {
                FreezeProjEntry existing = FrozenProjectiles[i];
                if (existing.ActivationId != activationId
                    || existing.Identity != target.Identity) {
                    continue;
                }
                if (existing.OwnerWho != ownerWho || existing.Duration != duration
                    || existing.Seed != target.Seed
                    || existing.FreezeCenter != target.Center) {
                    return false;
                }
                existing.Timer = Math.Max(existing.Timer, elapsed);
                if (existing.EntityIndex < 0) {
                    if (existing.ResolutionExpiresAt != 0
                        && Main.GameUpdateCount < existing.ResolutionExpiresAt) {
                        return true;
                    }
                    TimeControlReplicationSystem
                        .CancelProjectile<CyberDomainFreeze>(activationId,
                            target.Identity);
                    return QueueProjectileResolution(existing,
                        duration - existing.Timer);
                }
                return true;
            }

            FreezeProjEntry pending = new() {
                EntityIndex = -1,
                Identity = target.Identity,
                ActivationId = activationId,
                OwnerWho = ownerWho,
                Timer = elapsed,
                Duration = duration,
                Seed = target.Seed,
                FreezeCenter = target.Center,
            };
            FrozenProjectiles.Add(pending);
            return QueueProjectileResolution(pending, duration - elapsed);
        }

        private static bool QueueNPCResolution(FreezeEntry pending,
            int remainingFrames) {
            pending.ResolutionExpiresAt = ComputeResolutionExpiry(remainingFrames);
            EntityResolutionResult result = TimeControlReplicationSystem
                .ResolveOrQueueNPC<CyberDomainFreeze>(pending.ActivationId,
                    pending.Identity, remainingFrames,
                    npc => ResolvePendingNPC(pending, npc));
            if (result == EntityResolutionResult.Rejected) {
                FrozenNPCs.Remove(pending);
                return false;
            }
            return true;
        }

        private static bool QueueProjectileResolution(FreezeProjEntry pending,
            int remainingFrames) {
            pending.ResolutionExpiresAt = ComputeResolutionExpiry(remainingFrames);
            EntityResolutionResult result = TimeControlReplicationSystem
                .ResolveOrQueueProjectile<CyberDomainFreeze>(
                    pending.ActivationId, pending.Identity, remainingFrames,
                    projectile => ResolvePendingProjectile(pending, projectile));
            if (result == EntityResolutionResult.Rejected) {
                FrozenProjectiles.Remove(pending);
                return false;
            }
            return true;
        }

        private static void ResolvePendingNPC(FreezeEntry pending, NPC npc) {
            int index = FrozenNPCs.IndexOf(pending);
            if (index < 0 || WasNPCReleased(pending.ActivationId,
                pending.Identity)) {
                if (index >= 0) {
                    FrozenNPCs.RemoveAt(index);
                }
                return;
            }
            if (!ResolvePendingNPCEntry(pending, npc, replaceConflicts: true)) {
                FrozenNPCs.RemoveAt(index);
            }
        }

        private static void ResolvePendingProjectile(FreezeProjEntry pending,
            Projectile projectile) {
            int index = FrozenProjectiles.IndexOf(pending);
            if (index < 0 || WasProjectileReleased(pending.ActivationId,
                pending.Identity)) {
                if (index >= 0) {
                    FrozenProjectiles.RemoveAt(index);
                }
                return;
            }
            if (!ResolvePendingProjectileEntry(pending, projectile,
                replaceConflicts: true)) {
                FrozenProjectiles.RemoveAt(index);
            }
        }

        private static void PrepareIncomingNPCIdentity(long activationId,
            NetworkNPCIdentity identity) {
            for (int i = FrozenNPCs.Count - 1; i >= 0; i--) {
                FreezeEntry entry = FrozenNPCs[i];
                if (entry.Identity.Index == identity.Index
                    && (entry.ActivationId != activationId
                        || entry.Identity != identity)) {
                    RememberReleasedNPC(entry.ActivationId, entry.Identity);
                    RemoveNPCEntryAt(i, spawnBurst: false, broadcast: false);
                }
            }
        }

        private static bool RememberActivation(long activationId) {
            if (activationId <= 0 || !rememberedActivations.Add(activationId)) {
                return false;
            }
            activationOrder.Enqueue(activationId);
            while (activationOrder.Count > MaxRememberedActivations
                && activationOrder.TryDequeue(out long expired)) {
                rememberedActivations.Remove(expired);
            }
            return true;
        }

        private static void ClearRememberedActivations() {
            rememberedActivations.Clear();
            activationOrder.Clear();
        }

        private static bool WasNPCReleased(long activationId,
            NetworkNPCIdentity identity) {
            NPCReleaseRecord key = new(activationId, identity);
            return releasedNPCs.TryGetValue(key, out ulong expiresAt)
                && Main.GameUpdateCount < expiresAt;
        }

        private static bool WasProjectileReleased(long activationId,
            NetworkProjectileIdentity identity) {
            ProjectileReleaseRecord key = new(activationId, identity);
            return releasedProjectiles.TryGetValue(key, out ulong expiresAt)
                && Main.GameUpdateCount < expiresAt;
        }

        private static void RememberReleasedNPC(long activationId,
            NetworkNPCIdentity identity) {
            if (activationId <= 0 || !identity.IsValid) {
                return;
            }
            NPCReleaseRecord key = new(activationId, identity);
            EnsureReleasedCapacity(releasedNPCs, key, Main.maxNPCs);
            releasedNPCs[key] = Main.GameUpdateCount + ReleasedRetentionFrames;
        }

        private static void RememberReleasedProjectile(long activationId,
            NetworkProjectileIdentity identity) {
            if (activationId <= 0 || !identity.IsValid) {
                return;
            }
            ProjectileReleaseRecord key = new(activationId, identity);
            EnsureReleasedCapacity(releasedProjectiles, key,
                Main.maxProjectiles);
            releasedProjectiles[key] = Main.GameUpdateCount
                + ReleasedRetentionFrames;
        }

        private static void EnsureReleasedCapacity<TKey>(
            Dictionary<TKey, ulong> records, TKey incoming, int capacity)
            where TKey : notnull {
            if (records.ContainsKey(incoming) || records.Count < capacity) {
                return;
            }
            TKey oldestKey = default;
            ulong oldestExpiry = ulong.MaxValue;
            foreach ((TKey key, ulong expiresAt) in records) {
                if (expiresAt < oldestExpiry) {
                    oldestKey = key;
                    oldestExpiry = expiresAt;
                }
            }
            if (oldestExpiry != ulong.MaxValue) {
                records.Remove(oldestKey);
            }
        }

        private static void PruneReleasedTargets() {
            PruneReleasedTargets(releasedNPCs);
            PruneReleasedTargets(releasedProjectiles);
        }

        private static void PruneReleasedTargets<TKey>(
            Dictionary<TKey, ulong> records) where TKey : notnull {
            if (records.Count == 0) {
                return;
            }
            ulong now = Main.GameUpdateCount;
            List<TKey> expired = [];
            foreach ((TKey key, ulong expiresAt) in records) {
                if (now >= expiresAt) {
                    expired.Add(key);
                }
            }
            for (int i = 0; i < expired.Count; i++) {
                records.Remove(expired[i]);
            }
        }

        private static void ClearReleasedTargets() {
            releasedNPCs.Clear();
            releasedProjectiles.Clear();
        }

        internal static bool WriteSnapshot(BinaryWriter writer) {
            if (writer == null || Main.netMode == NetmodeID.MultiplayerClient) {
                return false;
            }
            List<FreezeEntry> npcEntries = [];
            List<FreezeProjEntry> projectileEntries = [];
            HashSet<NetworkNPCIdentity> npcIdentities = [];
            HashSet<int> npcIndices = [];
            for (int i = 0; i < FrozenNPCs.Count; i++) {
                FreezeEntry entry = FrozenNPCs[i];
                if (IsSnapshotEntryValid(entry) && IsEntryActive(entry)) {
                    if (!npcIdentities.Add(entry.Identity)
                        || !npcIndices.Add(entry.Identity.Index)) {
                        return false;
                    }
                    npcEntries.Add(entry);
                }
            }
            HashSet<NetworkProjectileIdentity> projectileIdentities = [];
            for (int i = 0; i < FrozenProjectiles.Count; i++) {
                FreezeProjEntry entry = FrozenProjectiles[i];
                if (IsSnapshotEntryValid(entry) && IsEntryActive(entry)) {
                    if (!projectileIdentities.Add(entry.Identity)) {
                        return false;
                    }
                    projectileEntries.Add(entry);
                }
            }
            if (npcEntries.Count > Main.maxNPCs
                || projectileEntries.Count > Main.maxProjectiles
                || npcEntries.Count + projectileEntries.Count
                    > Main.maxNPCs + Main.maxProjectiles) {
                return false;
            }

            try {
                writer.Write(SnapshotVersion);
                writer.Write((ushort)npcEntries.Count);
                for (int i = 0; i < npcEntries.Count; i++) {
                    WriteSnapshotEntry(writer, npcEntries[i]);
                }
                writer.Write((ushort)projectileEntries.Count);
                for (int i = 0; i < projectileEntries.Count; i++) {
                    WriteSnapshotEntry(writer, projectileEntries[i]);
                }
                return true;
            } catch (IOException) {
                return false;
            } catch (ObjectDisposedException) {
                return false;
            }
        }

        internal static bool ReadSnapshot(BinaryReader reader) {
            if (reader == null || Main.netMode != NetmodeID.MultiplayerClient) {
                return false;
            }
            try {
                if (reader.ReadByte() != SnapshotVersion) {
                    return false;
                }
                int npcCount = reader.ReadUInt16();
                if (npcCount > Main.maxNPCs) {
                    return false;
                }
                List<NPCSnapshotRecord> npcRecords = new(npcCount);
                HashSet<NetworkNPCIdentity> npcIdentities = [];
                HashSet<int> npcIndices = [];
                for (int i = 0; i < npcCount; i++) {
                    if (!TryReadNPCSnapshotEntry(reader, out NPCSnapshotRecord record)
                        || !npcIdentities.Add(record.Target.Identity)
                        || !npcIndices.Add(record.Target.Identity.Index)) {
                        return false;
                    }
                    npcRecords.Add(record);
                }

                int projectileCount = reader.ReadUInt16();
                if (projectileCount > Main.maxProjectiles
                    || npcCount + projectileCount
                        > Main.maxNPCs + Main.maxProjectiles) {
                    return false;
                }
                List<ProjectileSnapshotRecord> projectileRecords =
                    new(projectileCount);
                HashSet<NetworkProjectileIdentity> projectileIdentities = [];
                for (int i = 0; i < projectileCount; i++) {
                    if (!TryReadProjectileSnapshotEntry(reader,
                        out ProjectileSnapshotRecord record)
                        || !projectileIdentities.Add(record.Target.Identity)) {
                        return false;
                    }
                    projectileRecords.Add(record);
                }

                ReconcileReplicatedStateForSnapshot(npcRecords,
                    projectileRecords);
                for (int i = 0; i < npcRecords.Count; i++) {
                    NPCSnapshotRecord record = npcRecords[i];
                    RememberActivation(record.ActivationId);
                    ApplyFreezeBatch(record.OwnerWho, record.ActivationId,
                        new[] { record.Target },
                        Array.Empty<ProjectileFreezeTarget>(), replicated: true,
                        record.Elapsed, record.Duration, out _, out _);
                }
                for (int i = 0; i < projectileRecords.Count; i++) {
                    ProjectileSnapshotRecord record = projectileRecords[i];
                    RememberActivation(record.ActivationId);
                    ApplyFreezeBatch(record.OwnerWho, record.ActivationId,
                        Array.Empty<NPCFreezeTarget>(),
                        new[] { record.Target }, replicated: true,
                        record.Elapsed, record.Duration, out _, out _);
                }
                return true;
            } catch (EndOfStreamException) {
                return false;
            } catch (IOException) {
                return false;
            } catch (ObjectDisposedException) {
                return false;
            }
        }

        private static void ReconcileReplicatedStateForSnapshot(
            IReadOnlyList<NPCSnapshotRecord> npcRecords,
            IReadOnlyList<ProjectileSnapshotRecord> projectileRecords) {
            Dictionary<NPCReleaseRecord, NPCSnapshotRecord> expectedNPCs = [];
            for (int i = 0; i < npcRecords.Count; i++) {
                NPCSnapshotRecord record = npcRecords[i];
                expectedNPCs.Add(new NPCReleaseRecord(record.ActivationId,
                    record.Target.Identity), record);
            }
            Dictionary<ProjectileReleaseRecord, ProjectileSnapshotRecord>
                expectedProjectiles = [];
            for (int i = 0; i < projectileRecords.Count; i++) {
                ProjectileSnapshotRecord record = projectileRecords[i];
                expectedProjectiles.Add(new ProjectileReleaseRecord(
                    record.ActivationId, record.Target.Identity), record);
            }

            ClearRememberedActivations();
            ClearReleasedTargets();
            HashSet<NPCReleaseRecord> retainedNPCs = [];
            for (int i = FrozenNPCs.Count - 1; i >= 0; i--) {
                FreezeEntry entry = FrozenNPCs[i];
                NPCReleaseRecord key = new(entry.ActivationId, entry.Identity);
                if (!expectedNPCs.TryGetValue(key,
                    out NPCSnapshotRecord expected)) {
                    RememberReleasedNPC(entry.ActivationId, entry.Identity);
                    RemoveNPCEntryAt(i, spawnBurst: false, broadcast: false);
                    continue;
                }
                bool metadataMatches = entry.OwnerWho == expected.OwnerWho
                    && entry.Duration == expected.Duration
                    && entry.Seed == expected.Target.Seed
                    && entry.FreezeCenter == expected.Target.Center;
                if (!metadataMatches || !retainedNPCs.Add(key)) {
                    RemoveNPCEntryAt(i, spawnBurst: false, broadcast: false);
                }
            }

            HashSet<ProjectileReleaseRecord> retainedProjectiles = [];
            for (int i = FrozenProjectiles.Count - 1; i >= 0; i--) {
                FreezeProjEntry entry = FrozenProjectiles[i];
                ProjectileReleaseRecord key = new(entry.ActivationId,
                    entry.Identity);
                if (!expectedProjectiles.TryGetValue(key,
                    out ProjectileSnapshotRecord expected)) {
                    RememberReleasedProjectile(entry.ActivationId,
                        entry.Identity);
                    RemoveProjectileEntryAt(i, broadcast: false);
                    continue;
                }
                bool metadataMatches = entry.OwnerWho == expected.OwnerWho
                    && entry.Duration == expected.Duration
                    && entry.Seed == expected.Target.Seed
                    && entry.FreezeCenter == expected.Target.Center;
                if (!metadataMatches || !retainedProjectiles.Add(key)) {
                    RemoveProjectileEntryAt(i, broadcast: false);
                }
            }
        }

        private static bool TryReadNPCSnapshotEntry(BinaryReader reader,
            out NPCSnapshotRecord record) {
            record = default;
            int ownerWho = reader.ReadByte();
            long activationId = reader.ReadInt64();
            int elapsed = reader.ReadUInt16();
            int duration = reader.ReadUInt16();
            if (!TryReadNPCTarget(reader, out NPCFreezeTarget target)
                || !IsValidOwner(ownerWho) || activationId <= 0
                || !IsValidTiming(elapsed, duration)) {
                return false;
            }
            record = new NPCSnapshotRecord(ownerWho, activationId, elapsed,
                duration, target);
            return true;
        }

        private static bool TryReadProjectileSnapshotEntry(BinaryReader reader,
            out ProjectileSnapshotRecord record) {
            record = default;
            int ownerWho = reader.ReadByte();
            long activationId = reader.ReadInt64();
            int elapsed = reader.ReadUInt16();
            int duration = reader.ReadUInt16();
            if (!TryReadProjectileTarget(reader,
                out ProjectileFreezeTarget target)
                || !IsValidOwner(ownerWho) || activationId <= 0
                || !IsValidTiming(elapsed, duration)) {
                return false;
            }
            record = new ProjectileSnapshotRecord(ownerWho, activationId,
                elapsed, duration, target);
            return true;
        }

        private static void WriteSnapshotEntry(BinaryWriter writer,
            FreezeEntry entry) {
            writer.Write((byte)entry.OwnerWho);
            writer.Write(entry.ActivationId);
            writer.Write((ushort)entry.Timer);
            writer.Write((ushort)entry.Duration);
            entry.Identity.Write(writer);
            writer.Write(entry.Seed);
            writer.Write(entry.FreezeCenter.X);
            writer.Write(entry.FreezeCenter.Y);
        }

        private static void WriteSnapshotEntry(BinaryWriter writer,
            FreezeProjEntry entry) {
            writer.Write((byte)entry.OwnerWho);
            writer.Write(entry.ActivationId);
            writer.Write((ushort)entry.Timer);
            writer.Write((ushort)entry.Duration);
            entry.Identity.Write(writer);
            writer.Write(entry.Seed);
            writer.Write(entry.FreezeCenter.X);
            writer.Write(entry.FreezeCenter.Y);
        }

        private static bool IsSnapshotEntryValid(FreezeEntry entry)
            => entry != null && IsValidOwner(entry.OwnerWho)
            && entry.ActivationId > 0 && IsValidTiming(entry.Timer, entry.Duration)
            && IsValidTarget(new NPCFreezeTarget(entry.Identity, entry.Seed,
                entry.FreezeCenter));

        private static bool IsSnapshotEntryValid(FreezeProjEntry entry)
            => entry != null && IsValidOwner(entry.OwnerWho)
            && entry.ActivationId > 0 && IsValidTiming(entry.Timer, entry.Duration)
            && IsValidTarget(new ProjectileFreezeTarget(entry.Identity, entry.Seed,
                entry.FreezeCenter));

        private static bool TryReadNPCTarget(BinaryReader reader,
            out NPCFreezeTarget target) {
            target = default;
            if (!NetworkNPCIdentity.TryRead(reader,
                out NetworkNPCIdentity identity)) {
                return false;
            }
            float seed = reader.ReadSingle();
            Vector2 center = new(reader.ReadSingle(), reader.ReadSingle());
            target = new NPCFreezeTarget(identity, seed, center);
            return IsValidTarget(target);
        }

        private static bool TryReadProjectileTarget(BinaryReader reader,
            out ProjectileFreezeTarget target) {
            target = default;
            if (!NetworkProjectileIdentity.TryRead(reader,
                out NetworkProjectileIdentity identity)) {
                return false;
            }
            float seed = reader.ReadSingle();
            Vector2 center = new(reader.ReadSingle(), reader.ReadSingle());
            target = new ProjectileFreezeTarget(identity, seed, center);
            return IsValidTarget(target);
        }

        private static void WriteTarget(BinaryWriter writer,
            NPCFreezeTarget target) {
            target.Identity.Write(writer);
            writer.Write(target.Seed);
            writer.Write(target.Center.X);
            writer.Write(target.Center.Y);
        }

        private static void WriteTarget(BinaryWriter writer,
            ProjectileFreezeTarget target) {
            target.Identity.Write(writer);
            writer.Write(target.Seed);
            writer.Write(target.Center.X);
            writer.Write(target.Center.Y);
        }

        private static ModPacket NewPacket(FreezePacketKind kind) {
            ModPacket packet = CWRMod.Instance.GetPacket();
            packet.Write((byte)CWRMessageType.CyberDomainFreezeStart);
            packet.Write((byte)kind);
            return packet;
        }

        private static bool IsValidOwner(int ownerWho)
            => ownerWho >= 0 && ownerWho < Main.maxPlayers;

        private static bool IsValidTiming(int elapsed, int duration)
            => duration > 0 && duration <= DefaultFreezeDuration
            && elapsed >= 0 && elapsed < duration;

        private static bool IsValidTarget(NPCFreezeTarget target)
            => target.Identity.IsValid && IsValidSeed(target.Seed)
            && IsValidCenter(target.Center);

        private static bool IsValidTarget(ProjectileFreezeTarget target)
            => target.Identity.IsValid && IsValidSeed(target.Seed)
            && IsValidCenter(target.Center);

        private static bool IsValidSeed(float seed)
            => float.IsFinite(seed) && seed >= 0f && seed <= 1f;

        private static bool IsValidCenter(Vector2 center) {
            if (!float.IsFinite(center.X) || !float.IsFinite(center.Y)) {
                return false;
            }
            float maxX = Math.Max(Main.maxTilesX * 16f, 0f)
                + WorldCoordinateMargin;
            float maxY = Math.Max(Main.maxTilesY * 16f, 0f)
                + WorldCoordinateMargin;
            return center.X >= -WorldCoordinateMargin && center.X <= maxX
                && center.Y >= -WorldCoordinateMargin && center.Y <= maxY;
        }
    }
}

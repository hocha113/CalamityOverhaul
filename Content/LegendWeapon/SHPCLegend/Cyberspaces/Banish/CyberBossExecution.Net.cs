using CalamityOverhaul.Content.TimeFreezes;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces.Banish
{
    internal partial class CyberBossExecution
    {
        private enum ExecutionPacketKind : byte
        {
            Apply,
            Release,
        }

        private readonly record struct ExecutionRecord(
            long ActivationId,
            NetworkNPCIdentity Identity,
            int OwnerWho,
            int Elapsed,
            int SpawnedCount,
            int Damage,
            float Seed);

        private const byte SnapshotVersion = 1;
        private const int ReleasedRetentionFrames = 120;
        private static readonly Dictionary<long, ulong> releasedExecutions = [];

        internal static void HandleNetStart(BinaryReader reader, int whoAmI) {
            if (reader == null || Main.netMode != NetmodeID.MultiplayerClient) {
                return;
            }
            try {
                ExecutionPacketKind kind = (ExecutionPacketKind)reader.ReadByte();
                switch (kind) {
                    case ExecutionPacketKind.Apply:
                        if (TryReadExecution(reader, out ExecutionRecord record)) {
                            ApplyReplicatedExecution(record);
                        }
                        break;
                    case ExecutionPacketKind.Release:
                        ApplyExecutionRelease(reader.ReadInt64());
                        break;
                }
            } catch (EndOfStreamException) {
            } catch (IOException) {
            }
        }

        private static void SendExecutionApply(ExecutionEntry entry,
            int toWho = -1) {
            if (Main.netMode != NetmodeID.Server
                || !IsSerializableExecution(entry)) {
                return;
            }
            ModPacket packet = NewExecutionPacket(ExecutionPacketKind.Apply);
            WriteExecution(packet, entry);
            packet.Send(toWho);
        }

        private static void SendExecutionRelease(long activationId) {
            if (Main.netMode != NetmodeID.Server || activationId <= 0) {
                return;
            }
            ModPacket packet = NewExecutionPacket(ExecutionPacketKind.Release);
            packet.Write(activationId);
            packet.Send();
        }

        private static bool ApplyReplicatedExecution(ExecutionRecord record) {
            if (WasExecutionReleased(record.ActivationId)
                || !IsValidExecution(record)) {
                return false;
            }
            ExecutionEntry entry = FindExecution(record.ActivationId);
            if (entry == null) {
                RemoveConflictingExecution(record.ActivationId,
                    record.Identity);
                entry = new ExecutionEntry {
                    ActivationId = record.ActivationId,
                    Identity = record.Identity,
                    Timer = record.Elapsed,
                    SpawnedCount = record.SpawnedCount,
                    Damage = record.Damage,
                    OwnerWho = record.OwnerWho,
                    Seed = record.Seed,
                    Authoritative = false,
                    Resolved = false,
                };
                ActiveExecutions.Add(entry);
            }
            else if (entry.Identity != record.Identity
                || entry.OwnerWho != record.OwnerWho
                || entry.Damage != record.Damage
                || entry.Seed != record.Seed) {
                return false;
            }
            entry.Timer = Math.Max(entry.Timer, record.Elapsed);
            entry.SpawnedCount = Math.Max(entry.SpawnedCount,
                record.SpawnedCount);

            EntityResolutionResult resolution = TimeControlReplicationSystem
                .ResolveOrQueueNPC<CyberBossExecution>(record.ActivationId,
                    record.Identity, ExecutionDuration - entry.Timer,
                    npc => ResolveExecution(record.ActivationId,
                        record.Identity, npc));
            if (resolution == EntityResolutionResult.Rejected) {
                RemoveExecution(entry, broadcast: false);
                return false;
            }
            return true;
        }

        private static void ResolveExecution(long activationId,
            NetworkNPCIdentity identity, NPC npc) {
            ExecutionEntry entry = FindExecution(activationId);
            if (entry == null || entry.Identity != identity
                || !identity.TryResolve(out NPC resolved) || resolved != npc) {
                return;
            }
            bool firstResolve = !entry.Resolved;
            entry.Resolved = true;
            if (firstResolve) {
                PlayExecutionStart(npc);
            }
        }

        private static void RemoveConflictingExecution(long activationId,
            NetworkNPCIdentity identity) {
            for (int i = ActiveExecutions.Count - 1; i >= 0; i--) {
                ExecutionEntry entry = ActiveExecutions[i];
                if (entry.Identity.Index == identity.Index
                    && (entry.ActivationId != activationId
                        || entry.Identity != identity)) {
                    RemoveExecution(entry, broadcast: false);
                }
            }
        }

        private static void ApplyExecutionRelease(long activationId) {
            if (activationId <= 0) {
                return;
            }
            TimeControlReplicationSystem.Cancel<CyberBossExecution>(activationId);
            ExecutionEntry entry = FindExecution(activationId);
            if (entry != null) {
                RemoveExecution(entry, broadcast: false);
            }
            else {
                RememberReleasedExecution(activationId);
            }
        }

        internal static bool WriteSnapshot(BinaryWriter writer) {
            if (writer == null || Main.netMode == NetmodeID.MultiplayerClient) {
                return false;
            }
            List<ExecutionEntry> serializable = [];
            for (int i = 0; i < ActiveExecutions.Count; i++) {
                if (IsSerializableExecution(ActiveExecutions[i])) {
                    serializable.Add(ActiveExecutions[i]);
                }
            }
            if (serializable.Count > Main.maxNPCs) {
                return false;
            }

            try {
                writer.Write(SnapshotVersion);
                writer.Write((ushort)serializable.Count);
                for (int i = 0; i < serializable.Count; i++) {
                    WriteExecution(writer, serializable[i]);
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
                int count = reader.ReadUInt16();
                if (count < 0 || count > Main.maxNPCs) {
                    return false;
                }
                List<ExecutionRecord> records = new(count);
                HashSet<long> activationIds = [];
                HashSet<NetworkNPCIdentity> identities = [];
                HashSet<int> entityIndices = [];
                for (int i = 0; i < count; i++) {
                    if (!TryReadExecution(reader, out ExecutionRecord record)
                        || !activationIds.Add(record.ActivationId)
                        || !identities.Add(record.Identity)
                        || !entityIndices.Add(record.Identity.Index)) {
                        return false;
                    }
                    records.Add(record);
                }

                ResetReplicatedExecutionsForSnapshot();
                for (int i = 0; i < records.Count; i++) {
                    ApplyReplicatedExecution(records[i]);
                }
                return true;
            } catch (EndOfStreamException) {
                return false;
            } catch (IOException) {
                return false;
            }
        }

        private static void ResetReplicatedExecutionsForSnapshot() {
            for (int i = ActiveExecutions.Count - 1; i >= 0; i--) {
                RemoveExecution(ActiveExecutions[i], broadcast: false);
            }
            ActiveExecutions.Clear();
            TimeControlReplicationSystem.CancelAll<CyberBossExecution>();
            ClearReleasedExecutions();
        }

        private static void WriteExecution(BinaryWriter writer,
            ExecutionEntry entry) {
            writer.Write(entry.ActivationId);
            entry.Identity.Write(writer);
            writer.Write((byte)entry.OwnerWho);
            writer.Write((ushort)entry.Timer);
            writer.Write((ushort)(ExecutionDuration - entry.Timer));
            writer.Write((byte)entry.SpawnedCount);
            writer.Write(entry.Damage);
            writer.Write(entry.Seed);
        }

        private static bool TryReadExecution(BinaryReader reader,
            out ExecutionRecord record) {
            record = default;
            long activationId = reader.ReadInt64();
            if (!NetworkNPCIdentity.TryRead(reader,
                out NetworkNPCIdentity identity)) {
                return false;
            }
            int ownerWho = reader.ReadByte();
            int elapsed = reader.ReadUInt16();
            int remaining = reader.ReadUInt16();
            int spawnedCount = reader.ReadByte();
            int damage = reader.ReadInt32();
            float seed = reader.ReadSingle();
            if (remaining <= 0 || elapsed < 0
                || elapsed + remaining != ExecutionDuration) {
                return false;
            }
            record = new ExecutionRecord(activationId, identity, ownerWho,
                elapsed, spawnedCount, damage, seed);
            return IsValidExecution(record);
        }

        private static bool IsSerializableExecution(ExecutionEntry entry) {
            if (entry == null) {
                return false;
            }
            return IsValidExecution(new ExecutionRecord(entry.ActivationId,
                entry.Identity, entry.OwnerWho, entry.Timer,
                entry.SpawnedCount, entry.Damage, entry.Seed));
        }

        private static bool IsValidExecution(ExecutionRecord record)
            => record.ActivationId > 0 && record.Identity.IsValid
            && IsValidOwner(record.OwnerWho)
            && record.Elapsed >= 0 && record.Elapsed < ExecutionDuration
            && record.SpawnedCount >= 0
            && record.SpawnedCount <= TargetBoltCount
            && record.Damage >= 1 && record.Damage <= MaxExecutionDamage
            && float.IsFinite(record.Seed)
            && record.Seed >= 0f && record.Seed <= 1f;

        private static ModPacket NewExecutionPacket(ExecutionPacketKind kind) {
            ModPacket packet = CWRMod.Instance.GetPacket();
            packet.Write((byte)CWRMessageType.CyberBossExecutionStart);
            packet.Write((byte)kind);
            return packet;
        }

        private static bool IsValidOwner(int ownerWho)
            => ownerWho >= 0 && ownerWho < Main.maxPlayers;

        private static bool WasExecutionReleased(long activationId)
            => activationId > 0
            && releasedExecutions.TryGetValue(activationId,
                out ulong expiresAt)
            && Main.GameUpdateCount < expiresAt;

        private static void RememberReleasedExecution(long activationId) {
            if (activationId <= 0) {
                return;
            }
            releasedExecutions[activationId] = Main.GameUpdateCount
                + ReleasedRetentionFrames;
        }

        private static void PruneReleasedExecutions() {
            if (releasedExecutions.Count == 0) {
                return;
            }
            ulong now = Main.GameUpdateCount;
            List<long> expired = [];
            foreach ((long activationId, ulong expiresAt) in releasedExecutions) {
                if (now >= expiresAt) {
                    expired.Add(activationId);
                }
            }
            for (int i = 0; i < expired.Count; i++) {
                releasedExecutions.Remove(expired[i]);
            }
        }

        private static void ClearReleasedExecutions()
            => releasedExecutions.Clear();
    }
}

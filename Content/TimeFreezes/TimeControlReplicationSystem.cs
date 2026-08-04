using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces.Banish;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces.DomainFreeze;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.TimeFreezes
{
    internal enum EntityResolutionResult
    {
        Rejected,
        Applied,
        Queued,
    }

    internal sealed class TimeControlReplicationSystem : ModSystem
    {
        private readonly record struct SnapshotSection(
            byte Id, string Name, Func<BinaryWriter, bool> Write,
            Func<BinaryReader, bool> Read);

        private const uint SnapshotMagic = 0x31524354;
        private const byte SnapshotVersion = 1;
        private const int MaxSnapshotSections = 16;
        private const int MaxSectionBytes = 64 * 1024;

        private readonly record struct PendingNPCKey(
            Type SourceType, long ActivationId, NetworkNPCIdentity Identity);

        private readonly record struct PendingProjectileKey(
            Type SourceType, long ActivationId, NetworkProjectileIdentity Identity);

        private sealed record PendingNPC(ulong ExpiresAt, Action<NPC> Apply);
        private sealed record PendingProjectile(ulong ExpiresAt, Action<Projectile> Apply);

        private static readonly Dictionary<PendingNPCKey, PendingNPC> pendingNPCs = [];
        private static readonly Dictionary<PendingProjectileKey, PendingProjectile> pendingProjectiles = [];
        private static ulong nextDropLogFrame;

        private static int Capacity => Main.maxNPCs + Main.maxProjectiles;

        internal static EntityResolutionResult ResolveOrQueueNPC<TSource>(
            long activationId, NetworkNPCIdentity identity, int remainingFrames,
            Action<NPC> apply) {
            if (activationId == 0 || !identity.IsValid || remainingFrames <= 0
                || apply == null) {
                return EntityResolutionResult.Rejected;
            }
            if (identity.TryResolve(out NPC npc)) {
                apply(npc);
                return EntityResolutionResult.Applied;
            }

            PendingNPCKey key = new(typeof(TSource), activationId, identity);
            if (pendingNPCs.ContainsKey(key)) {
                return EntityResolutionResult.Queued;
            }
            if (!HasCapacity()) {
                LogDrop(typeof(TSource), activationId, identity.ToString());
                return EntityResolutionResult.Rejected;
            }

            pendingNPCs.Add(key, new PendingNPC(
                ComputeExpiry(remainingFrames), apply));
            return EntityResolutionResult.Queued;
        }

        internal static EntityResolutionResult ResolveOrQueueProjectile<TSource>(
            long activationId, NetworkProjectileIdentity identity, int remainingFrames,
            Action<Projectile> apply) {
            if (activationId == 0 || !identity.IsValid || remainingFrames <= 0
                || apply == null) {
                return EntityResolutionResult.Rejected;
            }
            if (identity.TryResolve(out Projectile projectile)) {
                apply(projectile);
                return EntityResolutionResult.Applied;
            }

            PendingProjectileKey key = new(typeof(TSource), activationId, identity);
            if (pendingProjectiles.ContainsKey(key)) {
                return EntityResolutionResult.Queued;
            }
            if (!HasCapacity()) {
                LogDrop(typeof(TSource), activationId, identity.ToString());
                return EntityResolutionResult.Rejected;
            }

            pendingProjectiles.Add(key, new PendingProjectile(
                ComputeExpiry(remainingFrames), apply));
            return EntityResolutionResult.Queued;
        }

        internal static void Cancel<TSource>(long activationId) {
            Type sourceType = typeof(TSource);
            pendingNPCs.RemoveWhere(pair => pair.Key.SourceType == sourceType
                && pair.Key.ActivationId == activationId);
            pendingProjectiles.RemoveWhere(pair => pair.Key.SourceType == sourceType
                && pair.Key.ActivationId == activationId);
        }

        internal static void CancelNPC<TSource>(long activationId,
            NetworkNPCIdentity identity) {
            pendingNPCs.Remove(new PendingNPCKey(typeof(TSource), activationId,
                identity));
        }

        internal static void CancelProjectile<TSource>(long activationId,
            NetworkProjectileIdentity identity) {
            pendingProjectiles.Remove(new PendingProjectileKey(typeof(TSource),
                activationId, identity));
        }

        internal static void CancelAll<TSource>() {
            Type sourceType = typeof(TSource);
            pendingNPCs.RemoveWhere(pair => pair.Key.SourceType == sourceType);
            pendingProjectiles.RemoveWhere(pair => pair.Key.SourceType == sourceType);
        }

        public override void PreUpdateEntities() {
            ResolveNPCs();
            ResolveProjectiles();
        }

        public override void NetSend(BinaryWriter writer) {
            if (writer == null || Main.netMode != NetmodeID.Server) {
                return;
            }

            SnapshotSection[] sections = GetSnapshotSections();
            writer.Write(SnapshotMagic);
            writer.Write(SnapshotVersion);
            writer.Write((byte)Math.Min(sections.Length, MaxSnapshotSections));
            for (int i = 0; i < sections.Length && i < MaxSnapshotSections; i++) {
                WriteSnapshotSection(writer, sections[i]);
            }
        }

        public override void NetReceive(BinaryReader reader) {
            if (reader == null || Main.netMode != NetmodeID.MultiplayerClient) {
                return;
            }

            try {
                if (reader.ReadUInt32() != SnapshotMagic
                    || reader.ReadByte() != SnapshotVersion) {
                    return;
                }

                int count = reader.ReadByte();
                if (count < 0 || count > MaxSnapshotSections) {
                    return;
                }

                Dictionary<byte, SnapshotSection> sections = [];
                SnapshotSection[] known = GetSnapshotSections();
                for (int i = 0; i < known.Length; i++) {
                    sections[known[i].Id] = known[i];
                }

                HashSet<byte> seen = [];
                for (int i = 0; i < count; i++) {
                    byte id = reader.ReadByte();
                    int length = reader.ReadUInt16();
                    if (length > MaxSectionBytes || !seen.Add(id)) {
                        return;
                    }

                    byte[] payload = reader.ReadBytes(length);
                    if (payload.Length != length) {
                        return;
                    }
                    if (!sections.TryGetValue(id, out SnapshotSection section)) {
                        continue;
                    }

                    try {
                        using MemoryStream stream = new(payload, writable: false);
                        using BinaryReader sectionReader = new(stream,
                            Encoding.UTF8, leaveOpen: false);
                        section.Read(sectionReader);
                    } catch (Exception exception) {
                        LogSnapshotFailure(section.Name, exception);
                    }
                }
            } catch (EndOfStreamException) {
            } catch (IOException) {
            }
        }

        public override void ClearWorld() => ClearPending();

        public override void OnWorldUnload() => ClearPending();

        private static void ResolveNPCs() {
            if (pendingNPCs.Count == 0) {
                return;
            }
            ulong now = Main.GameUpdateCount;
            List<PendingNPCKey> keys = [.. pendingNPCs.Keys];
            for (int i = 0; i < keys.Count; i++) {
                PendingNPCKey key = keys[i];
                if (!pendingNPCs.TryGetValue(key, out PendingNPC pending)) {
                    continue;
                }
                if (now >= pending.ExpiresAt) {
                    pendingNPCs.Remove(key);
                    LogDrop(key.SourceType, key.ActivationId, key.Identity.ToString());
                    continue;
                }
                if (!key.Identity.TryResolve(out NPC npc)) {
                    continue;
                }
                try {
                    pending.Apply(npc);
                } catch (Exception exception) {
                    CWRMod.Instance?.Logger.Warn(
                        $"Time control NPC apply failed [{key.SourceType.Name}:{key.ActivationId}] {key.Identity}: {exception}");
                }
                pendingNPCs.Remove(key);
            }
        }

        private static void ResolveProjectiles() {
            if (pendingProjectiles.Count == 0) {
                return;
            }
            ulong now = Main.GameUpdateCount;
            List<PendingProjectileKey> keys = [.. pendingProjectiles.Keys];
            for (int i = 0; i < keys.Count; i++) {
                PendingProjectileKey key = keys[i];
                if (!pendingProjectiles.TryGetValue(key,
                    out PendingProjectile pending)) {
                    continue;
                }
                if (now >= pending.ExpiresAt) {
                    pendingProjectiles.Remove(key);
                    LogDrop(key.SourceType, key.ActivationId, key.Identity.ToString());
                    continue;
                }
                if (!key.Identity.TryResolve(out Projectile projectile)) {
                    continue;
                }
                try {
                    pending.Apply(projectile);
                } catch (Exception exception) {
                    CWRMod.Instance?.Logger.Warn(
                        $"Time control projectile apply failed [{key.SourceType.Name}:{key.ActivationId}] {key.Identity}: {exception}");
                }
                pendingProjectiles.Remove(key);
            }
        }

        private static bool HasCapacity()
            => pendingNPCs.Count + pendingProjectiles.Count < Capacity;

        private static ulong ComputeExpiry(int remainingFrames) {
            int waitFrames = Math.Clamp(remainingFrames, 1, 120);
            return Main.GameUpdateCount + (ulong)waitFrames;
        }

        private static void LogDrop(Type sourceType, long activationId,
            string identity) {
            ulong now = Main.GameUpdateCount;
            if (now < nextDropLogFrame) {
                return;
            }
            nextDropLogFrame = now + 300;
            CWRMod.Instance?.Logger.Warn(
                $"Time control entity resolution dropped [{sourceType?.Name}:{activationId}] {identity}");
        }

        private static void ClearPending() {
            pendingNPCs.Clear();
            pendingProjectiles.Clear();
            nextDropLogFrame = 0;
        }

        private static SnapshotSection[] GetSnapshotSections() => [
            new(1, "CyberDomainFreeze", CyberDomainFreeze.WriteSnapshot,
                CyberDomainFreeze.ReadSnapshot),
            new(2, "CyberBanish", CyberBanish.WriteSnapshot,
                CyberBanish.ReadSnapshot),
            new(3, "CyberBossExecution", CyberBossExecution.WriteSnapshot,
                CyberBossExecution.ReadSnapshot),
        ];

        private static void WriteSnapshotSection(BinaryWriter writer,
            in SnapshotSection section) {
            using MemoryStream stream = new();
            bool success = false;
            try {
                using (BinaryWriter sectionWriter = new(stream, Encoding.UTF8,
                    leaveOpen: true)) {
                    success = section.Write(sectionWriter);
                    sectionWriter.Flush();
                }
            } catch (Exception exception) {
                LogSnapshotFailure(section.Name, exception);
            }

            if (!success || stream.Length > MaxSectionBytes) {
                LogSnapshotFailure(section.Name,
                    new InvalidDataException("snapshot section rejected"));
                writer.Write(section.Id);
                writer.Write((ushort)0);
                return;
            }

            writer.Write(section.Id);
            writer.Write((ushort)stream.Length);
            writer.Write(stream.GetBuffer(), 0, checked((int)stream.Length));
        }

        private static ulong nextSnapshotLogFrame;

        private static void LogSnapshotFailure(string section, Exception exception) {
            ulong now = Main.GameUpdateCount;
            if (now < nextSnapshotLogFrame) {
                return;
            }
            nextSnapshotLogFrame = now + 300;
            CWRMod.Instance?.Logger.Warn(
                $"Time control snapshot rejected [{section}] {exception.Message}");
        }
    }

    internal static class TimeControlPendingDictionaryExtensions
    {
        internal static void RemoveWhere<TKey, TValue>(
            this Dictionary<TKey, TValue> dictionary,
            Func<KeyValuePair<TKey, TValue>, bool> predicate) where TKey : notnull {
            if (dictionary.Count == 0) {
                return;
            }
            List<TKey> removed = [];
            foreach (KeyValuePair<TKey, TValue> pair in dictionary) {
                if (predicate(pair)) {
                    removed.Add(pair.Key);
                }
            }
            for (int i = 0; i < removed.Count; i++) {
                dictionary.Remove(removed[i]);
            }
        }
    }
}

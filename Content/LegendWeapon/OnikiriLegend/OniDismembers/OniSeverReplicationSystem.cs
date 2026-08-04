using CalamityOverhaul.Content.TimeFreezes;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniDismembers
{
    /// <summary>与渲染资源无关的肢解激活快照</summary>
    internal readonly record struct OniSeverActivationSnapshot(
        long ActivationId,
        int Owner,
        NetworkNPCIdentity Target,
        Vector2 Center,
        float Angle,
        float Scale,
        float HalfLength,
        float Width,
        int Elapsed,
        int Duration,
        int HoldFrames,
        bool PointMode)
    {
        internal bool IsValid => ActivationId > 0
            && Owner >= 0 && Owner < Main.maxPlayers
            && Target.IsValid
            && OniSeverReplicationSystem.IsValidWorldPosition(Center)
            && float.IsFinite(Angle)
            && float.IsFinite(Scale)
            && Scale >= OniSeverReplicationSystem.MinScale
            && Scale <= OniSeverReplicationSystem.MaxScale
            && float.IsFinite(HalfLength) && HalfLength >= 1f
            && HalfLength <= OniSeverReplicationSystem.MaxHalfLength
            && float.IsFinite(Width) && Width >= 1f
            && Width <= OniSeverReplicationSystem.MaxWidth
            && Duration > 0 && Duration <= OniSeverReplicationSystem.MaxDuration
            && Elapsed >= 0 && Elapsed < Duration
            && HoldFrames >= 0 && HoldFrames <= Duration;
    }

    /// <summary>鬼切肢解复制层，实时包和晚加入快照共用入口</summary>
    internal sealed class OniSeverReplicationSystem : ModSystem
    {
        private sealed record AuthoritativeActivation(
            long ActivationId,
            int Owner,
            NetworkNPCIdentity Target,
            Vector2 Center,
            float Angle,
            float Scale,
            float HalfLength,
            float Width,
            int Duration,
            int HoldFrames,
            bool PointMode,
            ulong StartedAt);

        internal const float MinScale = 0.05f;
        internal const float MaxScale = OnikiriOverride.MaxCompositeBladeScale;
        internal const float MaxHalfLength = 8192f;
        internal const float MaxWidth = 2048f;
        internal const int MaxDuration = 600;

        private const byte SnapshotVersion = 1;
        private const float WorldCoordinateMargin = 8192f;
        private static readonly List<AuthoritativeActivation> authoritative = [];
        private static readonly Dictionary<long, ulong> appliedUntil = [];
        private static readonly Dictionary<int, NetworkNPCIdentity> slotIdentities = [];
        private static long nextActivationId;

        internal static long AllocateActivationId() {
            if (Main.netMode == NetmodeID.MultiplayerClient) {
                return 0;
            }
            nextActivationId++;
            if (nextActivationId <= 0) {
                nextActivationId = 1;
            }
            return nextActivationId;
        }

        internal static bool Publish(long activationId, int owner,
            NetworkNPCIdentity target, in DismemberStroke stroke, float scale,
            int duration, int holdFrames, bool pointMode) {
            OniSeverActivationSnapshot snapshot = new(activationId, owner, target,
                stroke.Center, stroke.Angle, scale, stroke.HalfLength,
                stroke.Width, 0, duration, holdFrames, pointMode);
            if (Main.netMode == NetmodeID.MultiplayerClient || !snapshot.IsValid) {
                return false;
            }

            for (int i = 0; i < authoritative.Count; i++) {
                if (authoritative[i].ActivationId == activationId) {
                    return false;
                }
            }
            if (authoritative.Count >= Main.maxNPCs) {
                authoritative.RemoveAt(0);
            }
            authoritative.Add(new AuthoritativeActivation(activationId, owner,
                target, stroke.Center, stroke.Angle, scale, stroke.HalfLength,
                stroke.Width, duration, holdFrames, pointMode,
                Main.GameUpdateCount));
            return true;
        }

        internal static void ApplySnapshot(in OniSeverActivationSnapshot snapshot) {
            if (Main.netMode != NetmodeID.MultiplayerClient || !snapshot.IsValid
                || !RememberApplied(snapshot.ActivationId,
                    snapshot.Duration - snapshot.Elapsed)) {
                return;
            }

            int remaining = snapshot.Duration - snapshot.Elapsed;
            int remainingHold = Math.Max(snapshot.HoldFrames - snapshot.Elapsed, 0);
            OniSeverActivationSnapshot snapshotCopy = snapshot;
            TimeControlReplicationSystem.ResolveOrQueueNPC<OniSeverStrike>(
                snapshotCopy.ActivationId, snapshotCopy.Target, remaining, npc => {
                    PrepareTargetIdentity(snapshotCopy.Target, npc);
                    //持有者本地纸面已建立零伤害切口，避免重复加刀
                    if (snapshotCopy.PointMode && snapshotCopy.Owner == Main.myPlayer
                        && OniDismember.IsLocked(npc.whoAmI)) {
                        return;
                    }
                    DismemberStroke stroke = new(snapshotCopy.Center, snapshotCopy.Angle,
                        snapshotCopy.HalfLength, snapshotCopy.Width);
                    OniDismember.TriggerGroup(npc, in stroke, remaining,
                        remainingHold);
                });
        }

        internal static void PrepareTargetIdentity(NetworkNPCIdentity identity,
            NPC npc) {
            if (!identity.IsValid || npc?.active != true
                || npc.whoAmI != identity.Index) {
                return;
            }
            if (slotIdentities.TryGetValue(identity.Index,
                out NetworkNPCIdentity previous) && previous != identity) {
                OniDismember.ClearTarget(npc);
            }
            slotIdentities[identity.Index] = identity;
        }

        public override void PreUpdateEntities() {
            PruneAuthoritative();
            PruneApplied();
        }

        public override void NetSend(BinaryWriter writer) {
            List<OniSeverActivationSnapshot> snapshots = [];
            ulong now = Main.GameUpdateCount;
            int capacity = Math.Min(Main.maxNPCs, ushort.MaxValue);
            for (int i = 0; i < authoritative.Count && snapshots.Count < capacity; i++) {
                AuthoritativeActivation activation = authoritative[i];
                int elapsed = GetElapsed(activation, now);
                OniSeverActivationSnapshot snapshot = new(activation.ActivationId,
                    activation.Owner, activation.Target, activation.Center,
                    activation.Angle, activation.Scale, activation.HalfLength,
                    activation.Width, elapsed,
                    activation.Duration, activation.HoldFrames,
                    activation.PointMode);
                if (snapshot.IsValid) {
                    snapshots.Add(snapshot);
                }
            }

            writer.Write(SnapshotVersion);
            writer.Write((ushort)snapshots.Count);
            for (int i = 0; i < snapshots.Count; i++) {
                WriteSnapshot(writer, snapshots[i]);
            }
        }

        public override void NetReceive(BinaryReader reader) {
            try {
                if (reader.ReadByte() != SnapshotVersion) {
                    return;
                }
                int count = reader.ReadUInt16();
                if (count < 0 || count > Main.maxNPCs) {
                    return;
                }
                for (int i = 0; i < count; i++) {
                    if (TryReadSnapshot(reader, out OniSeverActivationSnapshot snapshot)) {
                        ApplySnapshot(in snapshot);
                    }
                }
            } catch (EndOfStreamException) {
            } catch (IOException) {
            }
        }

        public override void ClearWorld() => Reset();

        public override void OnWorldUnload() => Reset();

        internal static bool IsValidWorldPosition(Vector2 position) {
            if (!float.IsFinite(position.X) || !float.IsFinite(position.Y)) {
                return false;
            }
            float maxX = Math.Max(Main.maxTilesX * 16f, 1f) + WorldCoordinateMargin;
            float maxY = Math.Max(Main.maxTilesY * 16f, 1f) + WorldCoordinateMargin;
            return position.X >= -WorldCoordinateMargin && position.X <= maxX
                && position.Y >= -WorldCoordinateMargin && position.Y <= maxY;
        }

        private static bool RememberApplied(long activationId, int remaining) {
            ulong now = Main.GameUpdateCount;
            if (appliedUntil.TryGetValue(activationId, out ulong expiry)
                && expiry > now) {
                return false;
            }
            appliedUntil[activationId] = now + (ulong)Math.Clamp(remaining, 1,
                MaxDuration);
            return true;
        }

        private static void PruneAuthoritative() {
            if (authoritative.Count == 0) {
                return;
            }
            ulong now = Main.GameUpdateCount;
            for (int i = authoritative.Count - 1; i >= 0; i--) {
                AuthoritativeActivation activation = authoritative[i];
                if (GetElapsed(activation, now) >= activation.Duration
                    || !activation.Target.TryResolve(out _)) {
                    authoritative.RemoveAt(i);
                }
            }
        }

        private static void PruneApplied() {
            if (appliedUntil.Count == 0) {
                return;
            }
            ulong now = Main.GameUpdateCount;
            List<long> expired = [];
            foreach ((long activationId, ulong expiry) in appliedUntil) {
                if (expiry <= now) {
                    expired.Add(activationId);
                }
            }
            for (int i = 0; i < expired.Count; i++) {
                appliedUntil.Remove(expired[i]);
            }
        }

        private static int GetElapsed(AuthoritativeActivation activation,
            ulong now) {
            ulong raw = now >= activation.StartedAt ? now - activation.StartedAt : 0;
            return (int)Math.Min(raw, (ulong)activation.Duration);
        }

        private static void WriteSnapshot(BinaryWriter writer,
            in OniSeverActivationSnapshot snapshot) {
            writer.Write(snapshot.ActivationId);
            writer.Write((byte)snapshot.Owner);
            snapshot.Target.Write(writer);
            writer.Write(snapshot.Center.X);
            writer.Write(snapshot.Center.Y);
            writer.Write(snapshot.Angle);
            writer.Write(snapshot.Scale);
            writer.Write(snapshot.HalfLength);
            writer.Write(snapshot.Width);
            writer.Write((ushort)snapshot.Elapsed);
            writer.Write((ushort)snapshot.Duration);
            writer.Write((ushort)snapshot.HoldFrames);
            writer.Write(snapshot.PointMode);
        }

        private static bool TryReadSnapshot(BinaryReader reader,
            out OniSeverActivationSnapshot snapshot) {
            long activationId = reader.ReadInt64();
            int owner = reader.ReadByte();
            bool targetValid = NetworkNPCIdentity.TryRead(reader,
                out NetworkNPCIdentity target);
            Vector2 center = new(reader.ReadSingle(), reader.ReadSingle());
            float angle = reader.ReadSingle();
            float scale = reader.ReadSingle();
            float halfLength = reader.ReadSingle();
            float width = reader.ReadSingle();
            int elapsed = reader.ReadUInt16();
            int duration = reader.ReadUInt16();
            int holdFrames = reader.ReadUInt16();
            bool pointMode = reader.ReadBoolean();
            snapshot = new OniSeverActivationSnapshot(activationId, owner,
                target, center, angle, scale, halfLength, width, elapsed,
                duration, holdFrames, pointMode);
            return targetValid && snapshot.IsValid;
        }

        private static void Reset() {
            authoritative.Clear();
            appliedUntil.Clear();
            slotIdentities.Clear();
            nextActivationId = 0;
            TimeControlReplicationSystem.CancelAll<OniSeverStrike>();
        }
    }
}

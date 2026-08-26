using CalamityOverhaul.Content.RAMSystems;
using CalamityOverhaul.Content.TimeFreezes;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces.Banish
{
    //信道基类挂在网络分部上：请求/应用/解除共用一条通道
    internal partial class CyberBanish : CWRNetChannel
    {
        public override void Receive(BinaryReader reader, int whoAmI) => HandleNetStart(reader, whoAmI);

        private enum BanishPacketKind : byte
        {
            Request,
            Apply,
            Release,
        }

        private enum BanishResultCode : byte
        {
            Success,
            InvalidRequest,
            InvalidPlayer,
            InvalidState,
            InvalidTarget,
            OutsideDomain,
            TargetBusy,
            InsufficientRam,
            ConflictingRequest,
            ExpiredRequest,
        }

        private readonly record struct BanishActivationRecord(
            int OwnerWho,
            long ActivationId,
            bool IsBoss,
            NetworkNPCIdentity PrimaryIdentity,
            int Elapsed,
            bool ExecutionTriggered,
            List<BanishTargetRecord> Targets);

        private const ushort RamOperationId = RamNet.FirstExternalOperation + 1;
        private const byte SnapshotVersion = 1;
        private const int ReleasedRetentionFrames = 120;
        private const float WorldCoordinateMargin = 8192f;
        private const float MaxVelocityComponent = 4096f;

        private static readonly Dictionary<long, ulong> releasedActivations = [];

        private static void SendBanishRequest(RamRequestToken request,
            NetworkNPCIdentity identity) {
            if (Main.netMode != NetmodeID.MultiplayerClient || !request.IsValid
                || !identity.IsValid) {
                return;
            }
            ModPacket packet = NewPacket(BanishPacketKind.Request);
            packet.Write(request.SessionId);
            packet.Write(request.RequestId);
            identity.Write(packet);
            packet.Send();
        }

        internal static void HandleNetStart(BinaryReader reader, int whoAmI) {
            if (reader == null) {
                return;
            }
            try {
                BanishPacketKind kind = (BanishPacketKind)reader.ReadByte();
                if (Main.netMode == NetmodeID.Server) {
                    if (kind == BanishPacketKind.Request) {
                        HandleBanishRequest(reader, whoAmI);
                    }
                    return;
                }
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    return;
                }
                switch (kind) {
                    case BanishPacketKind.Apply:
                        HandleApply(reader);
                        break;
                    case BanishPacketKind.Release:
                        HandleRelease(reader);
                        break;
                }
            } catch (EndOfStreamException) {
            } catch (IOException) {
            }
        }

        private static void HandleBanishRequest(BinaryReader reader, int whoAmI) {
            uint sessionId = reader.ReadUInt32();
            uint requestId = reader.ReadUInt32();
            if (!NetworkNPCIdentity.TryRead(reader,
                out NetworkNPCIdentity identity)
                || !IsValidOwner(whoAmI)) {
                return;
            }
            Player owner = Main.player[whoAmI];
            if (owner?.active != true || requestId == 0) {
                RamNet.SendStateSnapshot(owner, whoAmI);
                return;
            }

            RamRequestDisposition disposition = RamSystem.ClassifyRequest(owner,
                sessionId, requestId, RamOperationId,
                out RamRequestResult previous);
            if (disposition == RamRequestDisposition.Replay) {
                ResendSuccessfulActivation(owner, sessionId, requestId, whoAmI);
                RamNet.SendRequestResult(owner, previous, whoAmI);
                return;
            }
            if (disposition == RamRequestDisposition.Invalid) {
                RamNet.SendStateSnapshot(owner, whoAmI);
                return;
            }
            if (disposition != RamRequestDisposition.New) {
                BanishResultCode code = disposition
                    == RamRequestDisposition.Conflict
                    ? BanishResultCode.ConflictingRequest
                    : BanishResultCode.ExpiredRequest;
                RamNet.SendRejectedRequest(owner, sessionId, requestId,
                    RamOperationId, (byte)code, whoAmI);
                return;
            }

            ExecuteAuthoritativeBanish(owner,
                new RamRequestToken(sessionId, requestId), identity, whoAmI);
        }

        private static void ResendSuccessfulActivation(Player owner,
            uint sessionId, uint requestId, int toWho) {
            for (int i = 0; i < activeActivations.Count; i++) {
                BanishActivation activation = activeActivations[i];
                if (activation.OwnerWho == owner.whoAmI
                    && activation.Request.SessionId == sessionId
                    && activation.Request.RequestId == requestId) {
                    SendApply(activation, toWho);
                    return;
                }
            }
        }

        private static void CompleteRamRequest(Player owner,
            RamRequestToken request, BanishResultCode code, float paid,
            int responseClient) {
            if (!request.IsValid || responseClient < 0
                || Main.netMode != NetmodeID.Server) {
                return;
            }
            if (RamSystem.CompleteRequest(owner, request, RamOperationId,
                (byte)code, paid, out RamRequestResult result)) {
                RamNet.SendRequestResult(owner, result, responseClient);
                return;
            }
            RamNet.SendStateSnapshot(owner, responseClient);
        }

        private static void SendApply(BanishActivation activation,
            int toWho = -1) {
            if (Main.netMode != NetmodeID.Server
                || !IsSerializableActivation(activation)) {
                return;
            }
            ModPacket packet = NewPacket(BanishPacketKind.Apply);
            WriteActivation(packet, activation);
            packet.Send(toWho);
        }

        private static void SendRelease(long activationId) {
            if (Main.netMode != NetmodeID.Server || activationId <= 0) {
                return;
            }
            ModPacket packet = NewPacket(BanishPacketKind.Release);
            packet.Write(activationId);
            packet.Send();
        }

        private static void HandleApply(BinaryReader reader) {
            if (!TryReadActivation(reader, out BanishActivationRecord record)) {
                return;
            }
            ApplyReplicatedActivation(record.OwnerWho, record.ActivationId,
                record.IsBoss, record.PrimaryIdentity, record.Elapsed,
                record.ExecutionTriggered, record.Targets);
        }

        private static void HandleRelease(BinaryReader reader) {
            long activationId = reader.ReadInt64();
            if (activationId <= 0) {
                return;
            }
            ApplyRelease(activationId, spawnBurst: true);
        }

        private static void ApplyRelease(long activationId, bool spawnBurst) {
            TimeControlReplicationSystem.Cancel<CyberBanish>(activationId);
            BanishActivation activation = FindActivation(activationId);
            if (activation != null) {
                EndActivation(activation, broadcast: false, spawnBurst);
            }
            else {
                RememberReleased(activationId);
            }
        }

        internal static bool WriteSnapshot(BinaryWriter writer) {
            if (writer == null || Main.netMode == NetmodeID.MultiplayerClient) {
                return false;
            }
            List<BanishActivation> serializable = [];
            for (int i = 0; i < activeActivations.Count; i++) {
                if (IsSerializableActivation(activeActivations[i])) {
                    serializable.Add(activeActivations[i]);
                }
            }
            if (serializable.Count > Main.maxNPCs) {
                return false;
            }

            try {
                writer.Write(SnapshotVersion);
                writer.Write((ushort)serializable.Count);
                for (int i = 0; i < serializable.Count; i++) {
                    WriteActivation(writer, serializable[i]);
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
                List<BanishActivationRecord> records = new(count);
                HashSet<long> activationIds = [];
                HashSet<NetworkNPCIdentity> identities = [];
                HashSet<int> entityIndices = [];
                int totalTargets = 0;
                for (int i = 0; i < count; i++) {
                    if (!TryReadActivation(reader,
                        out BanishActivationRecord record)
                        || !activationIds.Add(record.ActivationId)) {
                        return false;
                    }
                    totalTargets += record.Targets.Count;
                    if (totalTargets > Main.maxNPCs) {
                        return false;
                    }
                    for (int j = 0; j < record.Targets.Count; j++) {
                        if (!identities.Add(record.Targets[j].Identity)
                            || !entityIndices.Add(
                                record.Targets[j].Identity.Index)) {
                            return false;
                        }
                    }
                    records.Add(record);
                }

                ResetReplicatedStateForSnapshot();
                for (int i = 0; i < records.Count; i++) {
                    BanishActivationRecord record = records[i];
                    ApplyReplicatedActivation(record.OwnerWho,
                        record.ActivationId, record.IsBoss,
                        record.PrimaryIdentity, record.Elapsed,
                        record.ExecutionTriggered, record.Targets);
                }
                return true;
            } catch (EndOfStreamException) {
                return false;
            } catch (IOException) {
                return false;
            }
        }

        private static void ResetReplicatedStateForSnapshot() {
            for (int i = activeActivations.Count - 1; i >= 0; i--) {
                EndActivation(activeActivations[i], broadcast: false,
                    spawnBurst: false);
            }
            activeActivations.Clear();
            ActiveBanishments.Clear();
            TimeControlReplicationSystem.CancelAll<CyberBanish>();
            ClearReleasedActivations();
        }

        private static void WriteActivation(BinaryWriter writer,
            BanishActivation activation) {
            writer.Write((byte)activation.OwnerWho);
            writer.Write(activation.ActivationId);
            writer.Write(activation.IsBoss);
            activation.PrimaryIdentity.Write(writer);
            writer.Write((ushort)activation.Timer);
            writer.Write((ushort)(BanishDuration - activation.Timer));
            writer.Write(activation.ExecutionTriggered);
            writer.Write((ushort)activation.Targets.Count);
            for (int i = 0; i < activation.Targets.Count; i++) {
                WriteTarget(writer, activation.Targets[i]);
            }
        }

        private static bool TryReadActivation(BinaryReader reader,
            out BanishActivationRecord record) {
            record = default;
            int ownerWho = reader.ReadByte();
            long activationId = reader.ReadInt64();
            bool isBoss = reader.ReadBoolean();
            if (!NetworkNPCIdentity.TryRead(reader,
                out NetworkNPCIdentity primaryIdentity)) {
                return false;
            }
            int elapsed = reader.ReadUInt16();
            int remaining = reader.ReadUInt16();
            bool executionTriggered = reader.ReadBoolean();
            int targetCount = reader.ReadUInt16();
            if (remaining <= 0 || elapsed < 0
                || elapsed + remaining != BanishDuration
                || targetCount <= 0 || targetCount > Main.maxNPCs) {
                return false;
            }

            List<BanishTargetRecord> targets = new(targetCount);
            HashSet<NetworkNPCIdentity> identities = [];
            for (int i = 0; i < targetCount; i++) {
                if (!TryReadTarget(reader, out BanishTargetRecord target)
                    || !identities.Add(target.Identity)) {
                    return false;
                }
                targets.Add(target);
            }
            if (!IsValidActivation(ownerWho, activationId, elapsed,
                BanishDuration, isBoss, primaryIdentity, targets)) {
                return false;
            }
            record = new BanishActivationRecord(ownerWho, activationId,
                isBoss, primaryIdentity, elapsed, executionTriggered, targets);
            return true;
        }

        private static void WriteTarget(BinaryWriter writer,
            BanishTargetRecord target) {
            target.Identity.Write(writer);
            writer.Write(target.Seed);
            writer.Write(target.Center.X);
            writer.Write(target.Center.Y);
            writer.Write(target.ResumeVelocity.X);
            writer.Write(target.ResumeVelocity.Y);
            writer.Write(target.IsPrimary);
        }

        private static bool TryReadTarget(BinaryReader reader,
            out BanishTargetRecord target) {
            target = default;
            if (!NetworkNPCIdentity.TryRead(reader,
                out NetworkNPCIdentity identity)) {
                return false;
            }
            float seed = reader.ReadSingle();
            Vector2 center = new(reader.ReadSingle(), reader.ReadSingle());
            Vector2 resumeVelocity = new(reader.ReadSingle(), reader.ReadSingle());
            bool isPrimary = reader.ReadBoolean();
            target = new BanishTargetRecord(identity, seed, center,
                resumeVelocity, isPrimary);
            return IsValidTarget(target);
        }

        private static bool IsSerializableActivation(BanishActivation activation)
            => activation != null
            && IsValidActivation(activation.OwnerWho, activation.ActivationId,
                activation.Timer, BanishDuration, activation.IsBoss,
                activation.PrimaryIdentity, activation.Targets);

        private static bool IsValidActivation(int ownerWho, long activationId,
            int elapsed, int duration, bool isBoss,
            NetworkNPCIdentity primaryIdentity,
            IReadOnlyList<BanishTargetRecord> targets) {
            if (!IsValidOwner(ownerWho) || activationId <= 0
                || duration != BanishDuration || elapsed < 0
                || elapsed >= duration || !primaryIdentity.IsValid
                || targets == null || targets.Count <= 0
                || targets.Count > Main.maxNPCs) {
                return false;
            }
            HashSet<NetworkNPCIdentity> identities = [];
            HashSet<int> indices = [];
            int primaryCount = 0;
            for (int i = 0; i < targets.Count; i++) {
                BanishTargetRecord target = targets[i];
                if (!IsValidTarget(target) || !identities.Add(target.Identity)
                    || !indices.Add(target.Identity.Index)) {
                    return false;
                }
                if (target.IsPrimary) {
                    primaryCount++;
                    if (target.Identity != primaryIdentity) {
                        return false;
                    }
                }
            }
            return primaryCount == 1 && identities.Contains(primaryIdentity);
        }

        private static bool IsValidTarget(BanishTargetRecord target)
            => target.Identity.IsValid
            && float.IsFinite(target.Seed)
            && target.Seed >= 0f && target.Seed <= 1f
            && IsValidCenter(target.Center)
            && float.IsFinite(target.ResumeVelocity.X)
            && float.IsFinite(target.ResumeVelocity.Y)
            && MathF.Abs(target.ResumeVelocity.X) <= MaxVelocityComponent
            && MathF.Abs(target.ResumeVelocity.Y) <= MaxVelocityComponent;

        private static bool IsValidOwner(int ownerWho)
            => ownerWho >= 0 && ownerWho < Main.maxPlayers;

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

        private static ModPacket NewPacket(BanishPacketKind kind) {
            ModPacket packet = CWRNetWork.GetPacket<CyberBanish>();
            packet.Write((byte)kind);
            return packet;
        }

        private static bool WasReleased(long activationId)
            => activationId > 0 && releasedActivations.TryGetValue(activationId,
                out ulong expiresAt) && Main.GameUpdateCount < expiresAt;

        private static void RememberReleased(long activationId) {
            if (activationId <= 0) {
                return;
            }
            releasedActivations[activationId] = Main.GameUpdateCount
                + ReleasedRetentionFrames;
        }

        private static void PruneReleasedActivations() {
            if (releasedActivations.Count == 0) {
                return;
            }
            ulong now = Main.GameUpdateCount;
            List<long> expired = [];
            foreach ((long activationId, ulong expiresAt) in releasedActivations) {
                if (now >= expiresAt) {
                    expired.Add(activationId);
                }
            }
            for (int i = 0; i < expired.Count; i++) {
                releasedActivations.Remove(expired[i]);
            }
        }

        private static void ClearReleasedActivations()
            => releasedActivations.Clear();
    }
}

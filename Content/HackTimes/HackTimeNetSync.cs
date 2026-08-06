using CalamityOverhaul.Content.HackTimes.Scannables;
using CalamityOverhaul.Content.RAMSystems;
using CalamityOverhaul.Content.TimeFreezes;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.HackTimes
{
    internal enum HackNetOperation : byte
    {
        Request,
        QueueState,
        EffectApply,
        EffectProgress,
        EffectRemove,
    }

    internal enum HackRequestResultCode : byte
    {
        Success,
        InvalidSession,
        ConflictingRequest,
        ExpiredRequest,
        InvalidPlayer,
        InvalidProtocol,
        InvalidTarget,
        UnsupportedTarget,
        Unavailable,
        InsufficientRam,
        QueueFull,
        InvalidPayload,
    }

    internal readonly record struct HackNetworkTarget(
        HackTargetKind Kind,
        NetworkNPCIdentity NpcIdentity,
        int TileX,
        int TileY)
    {
        internal bool IsSerializable => Kind switch {
            HackTargetKind.Npc => NpcIdentity.IsValid,
            HackTargetKind.Tile => TileX >= 0 && TileX < Main.maxTilesX
                && TileY >= 0 && TileY < Main.maxTilesY,
            _ => false,
        };

        internal bool TryResolve(out IHackTarget target) {
            target = null;
            if (!IsSerializable) return false;
            if (Kind == HackTargetKind.Npc) {
                if (!NpcIdentity.TryResolve(out NPC npc)) return false;
                target = new NpcScannable(npc.whoAmI);
                return true;
            }
            if (Kind == HackTargetKind.Tile) {
                target = new TileScannable(TileX, TileY);
                return true;
            }
            return false;
        }

        internal static bool TryCapture(IHackTarget target,
            out HackNetworkTarget identity) {
            identity = default;
            if (target is NpcScannable npcTarget) {
                if (npcTarget.NpcIndex < 0 || npcTarget.NpcIndex >= Main.maxNPCs
                    || !NetworkNPCIdentity.TryCapture(Main.npc[npcTarget.NpcIndex],
                        out NetworkNPCIdentity npcIdentity)) {
                    return false;
                }
                identity = new HackNetworkTarget(HackTargetKind.Npc,
                    npcIdentity, -1, -1);
                return true;
            }
            if (target is TileScannable tileTarget) {
                int x = tileTarget.TileCoordX;
                int y = tileTarget.TileCoordY;
                if (x < 0 || x >= Main.maxTilesX || y < 0
                    || y >= Main.maxTilesY || !Main.tile[x, y].HasTile) {
                    return false;
                }
                identity = new HackNetworkTarget(HackTargetKind.Tile,
                    default, x, y);
                return true;
            }
            return false;
        }
    }

    internal readonly record struct HackQueueSnapshotRecord(
        int PlayerIndex,
        uint SessionId,
        uint RequestId,
        int SlotIndex,
        HackQueueState State,
        int Elapsed,
        int UploadFrames,
        long ActivationId,
        HackNetworkTarget Target);

    internal readonly record struct HackEffectSnapshotRecord(
        long ActivationId,
        int CasterIndex,
        uint SessionId,
        uint RequestId,
        int SlotIndex,
        int Elapsed,
        float EffectMult,
        int Generation,
        HackNetworkTarget Target);

    /// <summary>骇入请求、队列与效果复制</summary>
    internal static class HackTimeNetSync
    {
        internal const ushort RamOperationId = 48;
        private const byte SnapshotVersion = 1;
        private const int MaxUploadsPerPlayer = 32;
        private const int MaxSnapshotUploads = 2048;
        private const int MaxSnapshotEffects = 4096;
        private const int MaxUploadFrames = 60 * 60;
        private const float MaxTargetDistance = 6400f;
        private const float MaxClaimError = 512f;

        private sealed class HackEffectPendingSource { }
        private sealed class HackQueuePendingSource { }

        private static readonly Dictionary<long, int> pendingProgress = [];
        private static long nextActivationId;
        private static ulong lastAuthorityUpdateFrame = ulong.MaxValue;

        internal static bool TryRequestQueue(QuickHackDef hack, IHackTarget target,
            out uint sessionId, out uint requestId) {
            sessionId = 0;
            requestId = 0;
            if (hack == null || target == null || !target.IsValid
                || hack.SlotIndex < 0 || hack.SlotIndex >= QuickHackDef.Count
                || (hack.SupportedTargets & (target.TargetType?.Kind
                    ?? HackTargetKind.None)) == 0) {
                return false;
            }
            if (Main.myPlayer < 0 || Main.myPlayer >= Main.maxPlayers) return false;
            Player player = Main.player[Main.myPlayer];
            if (player?.active != true || player.dead
                || !RamSystem.TryAllocateRequest(player, out RamRequestToken token)) {
                return false;
            }
            sessionId = token.SessionId;
            requestId = token.RequestId;

            if (Main.netMode == NetmodeID.SinglePlayer) {
                return ExecuteAuthorityRequest(player, token, hack, target,
                    default, target.WorldCenter, -1);
            }
            if (Main.netMode != NetmodeID.MultiplayerClient
                || !HackNetworkTarget.TryCapture(target,
                    out HackNetworkTarget targetIdentity)
                || !IsFiniteVector(target.WorldCenter)) {
                return false;
            }

            ModPacket packet = NewPacket(HackNetOperation.Request);
            packet.Write(token.SessionId);
            packet.Write(token.RequestId);
            packet.Write((ushort)hack.SlotIndex);
            WriteTarget(packet, targetIdentity);
            packet.Write(target.WorldCenter.X);
            packet.Write(target.WorldCenter.Y);
            packet.Send();
            return true;
        }

        internal static void NetHandle(CWRMessageType type, BinaryReader reader,
            int whoAmI) {
            if (type == CWRMessageType.HackProtocolApply)
                HandleApplyPacket(reader, whoAmI);
        }

        public static void HandleApplyPacket(BinaryReader reader, int whoAmI) {
            if (reader == null) return;
            try {
                HackNetOperation operation = (HackNetOperation)reader.ReadByte();
                switch (operation) {
                    case HackNetOperation.Request:
                        HandleRequest(reader, whoAmI);
                        break;
                    case HackNetOperation.QueueState:
                        HandleQueueState(reader);
                        break;
                    case HackNetOperation.EffectApply:
                        HandleEffectApply(reader);
                        break;
                    case HackNetOperation.EffectProgress:
                        HandleEffectProgress(reader);
                        break;
                    case HackNetOperation.EffectRemove:
                        HandleEffectRemove(reader);
                        break;
                }
            } catch (EndOfStreamException) {
            } catch (IOException) {
            }
        }

        private static void HandleRequest(BinaryReader reader, int whoAmI) {
            uint sessionId = reader.ReadUInt32();
            uint requestId = reader.ReadUInt32();
            int slotIndex = reader.ReadUInt16();
            if (!TryReadTarget(reader, out HackNetworkTarget identity)) return;
            Vector2 claimedCenter = new(reader.ReadSingle(), reader.ReadSingle());
            if (Main.netMode != NetmodeID.Server || !IsValidPlayerIndex(whoAmI))
                return;
            Player player = Main.player[whoAmI];
            if (player?.active != true || requestId == 0) {
                RamNet.SendStateSnapshot(player, whoAmI);
                return;
            }

            RamRequestDisposition disposition = RamSystem.ClassifyRequest(player,
                sessionId, requestId, RamOperationId,
                out RamRequestResult previous);
            if (disposition == RamRequestDisposition.Replay) {
                ResendRequestState(player, sessionId, requestId, whoAmI);
                RamNet.SendRequestResult(player, previous, whoAmI);
                return;
            }
            if (disposition == RamRequestDisposition.Invalid) {
                RamNet.SendStateSnapshot(player, whoAmI);
                return;
            }
            if (disposition != RamRequestDisposition.New) {
                HackRequestResultCode code = disposition
                    == RamRequestDisposition.Conflict
                    ? HackRequestResultCode.ConflictingRequest
                    : HackRequestResultCode.ExpiredRequest;
                RamNet.SendRejectedRequest(player, sessionId, requestId,
                    RamOperationId, (byte)code, whoAmI);
                return;
            }

            if (!identity.TryResolve(out IHackTarget target)) {
                CompleteRequest(player, new RamRequestToken(sessionId, requestId),
                    HackRequestResultCode.InvalidTarget, 0f, whoAmI);
                return;
            }
            QuickHackDef hack = QuickHackDef.GetByIndex(slotIndex);
            ExecuteAuthorityRequest(player,
                new RamRequestToken(sessionId, requestId), hack, target,
                identity, claimedCenter, whoAmI);
        }

        private static bool ExecuteAuthorityRequest(Player player,
            RamRequestToken request, QuickHackDef hack, IHackTarget target,
            HackNetworkTarget identity, Vector2 claimedCenter,
            int responseClient) {
            if (Main.netMode == NetmodeID.MultiplayerClient || player?.active != true
                || player.dead || !request.IsValid) return false;

            HackRequestResultCode failure = ValidateAuthorityRequest(player, hack,
                target, identity, claimedCenter);
            if (failure != HackRequestResultCode.Success) {
                CompleteRequest(player, request, failure, 0f, responseClient);
                SendQueueState(player.whoAmI, request.SessionId,
                    request.RequestId, hack?.SlotIndex ?? -1,
                    HackQueueState.Waiting, 0, 1, 0, identity,
                    accepted: false, responseClient);
                return false;
            }

            HackTimeAuthorityPlayer state = player
                .GetModPlayer<HackTimeAuthorityPlayer>();
            state.BindSession(request.SessionId);
            if (state.Uploads.Count >= MaxUploadsPerPlayer) {
                CompleteRequest(player, request, HackRequestResultCode.QueueFull,
                    0f, responseClient);
                SendQueueState(player.whoAmI, request.SessionId,
                    request.RequestId, hack.SlotIndex, HackQueueState.Waiting,
                    0, hack.UploadTime, 0, identity, accepted: false,
                    responseClient);
                return false;
            }
            for (int i = 0; i < state.Uploads.Count; i++) {
                AuthorityHackUpload queued = state.Uploads[i];
                if (queued.SlotIndex == hack.SlotIndex
                    && queued.Target?.TargetEquals(target) == true) {
                    CompleteRequest(player, request,
                        HackRequestResultCode.Unavailable, 0f, responseClient);
                    SendQueueState(player.whoAmI, request.SessionId,
                        request.RequestId, hack.SlotIndex,
                        HackQueueState.Waiting, 0, hack.UploadTime, 0,
                        identity, accepted: false, responseClient);
                    return false;
                }
            }

            int cost = Math.Clamp(HackCostEvaluator.GetActualCost(hack, target,
                player),
                1, (int)RamSystem.MaxMutationAmount);
            float paid = 0f;
            bool infiniteAuthority = HackTime.InfiniteHackAuthority;
            if (!infiniteAuthority && !RamSystem.TryConsume(player, cost, out paid)) {
                CompleteRequest(player, request,
                    HackRequestResultCode.InsufficientRam, 0f, responseClient);
                SendQueueState(player.whoAmI, request.SessionId,
                    request.RequestId, hack.SlotIndex, HackQueueState.Waiting,
                    0, hack.UploadTime, 0, identity, accepted: false,
                    responseClient);
                return false;
            }

            var upload = new AuthorityHackUpload {
                SessionId = request.SessionId,
                RequestId = request.RequestId,
                SlotIndex = hack.SlotIndex,
                Target = target,
                TargetIdentity = identity,
                PaidRamCost = paid,
                Elapsed = 0,
                UploadFrames = Math.Clamp(hack.UploadTime, 1, MaxUploadFrames),
                State = state.Uploads.Count == 0
                    ? HackQueueState.Uploading
                    : HackQueueState.Waiting,
                ActivationId = 0,
            };
            state.Uploads.Add(upload);

            if (!CompleteRequest(player, request, HackRequestResultCode.Success,
                paid, responseClient)) {
                state.Uploads.Remove(upload);
                if (paid > 0f) RamSystem.Restore(player, paid, out _);
                return false;
            }
            SendQueueState(player.whoAmI, upload.SessionId, upload.RequestId,
                upload.SlotIndex, upload.State, upload.Elapsed,
                upload.UploadFrames, upload.ActivationId, identity,
                accepted: true, responseClient);
            return true;
        }

        private static HackRequestResultCode ValidateAuthorityRequest(Player player,
            QuickHackDef hack, IHackTarget target, HackNetworkTarget identity,
            Vector2 claimedCenter) {
            if (player == null || !player.active || player.dead)
                return HackRequestResultCode.InvalidPlayer;
            if (hack == null || hack.SlotIndex < 0
                || hack.SlotIndex >= QuickHackDef.Count
                || hack.UploadTime <= 0 || hack.UploadTime > MaxUploadFrames
                || hack.RamCost < 0 || hack.RamCost > RamSystem.MaxMutationAmount) {
                return HackRequestResultCode.InvalidProtocol;
            }
            if (target == null || !target.IsValid || !target.IsHackable)
                return HackRequestResultCode.InvalidTarget;
            HackTargetKind kind = target.TargetType?.Kind ?? HackTargetKind.None;
            if ((hack.SupportedTargets & kind) == 0)
                return HackRequestResultCode.UnsupportedTarget;
            if (Main.netMode == NetmodeID.Server
                && (!identity.IsSerializable || identity.Kind != kind)) {
                return HackRequestResultCode.InvalidTarget;
            }
            Vector2 center = target.WorldCenter;
            if (!IsFiniteVector(center) || !IsFiniteVector(claimedCenter)
                || Vector2.DistanceSquared(center, claimedCenter)
                    > MaxClaimError * MaxClaimError
                || Vector2.DistanceSquared(player.Center, center)
                    > MaxTargetDistance * MaxTargetDistance) {
                return HackRequestResultCode.InvalidPayload;
            }
            if (!HackTimeAccess.CanUse(player) || !hack.CanApplyTo(target, player))
                return HackRequestResultCode.Unavailable;
            return HackRequestResultCode.Success;
        }

        private static bool CompleteRequest(Player player, RamRequestToken request,
            HackRequestResultCode code, float paid, int responseClient) {
            if (!RamSystem.CompleteRequest(player, request, RamOperationId,
                (byte)code, paid, out RamRequestResult result)) {
                if (Main.netMode == NetmodeID.Server && responseClient >= 0)
                    RamNet.SendStateSnapshot(player, responseClient);
                return false;
            }
            if (Main.netMode == NetmodeID.Server && responseClient >= 0)
                RamNet.SendRequestResult(player, result, responseClient);
            return true;
        }

        internal static void UpdateAuthority() {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;
            ulong frame = Main.GameUpdateCount;
            if (lastAuthorityUpdateFrame == frame) return;
            lastAuthorityUpdateFrame = frame;

            for (int playerIndex = 0; playerIndex < Main.maxPlayers; playerIndex++) {
                Player player = Main.player[playerIndex];
                if (player?.active != true) continue;
                HackTimeAuthorityPlayer state = player
                    .GetModPlayer<HackTimeAuthorityPlayer>();
                RAMPlayer ram = player.GetModPlayer<RAMPlayer>();
                if (!ram.ProfileInitialized || ram.SessionId == 0) continue;
                state.BindSession(ram.SessionId);
                UpdatePlayerUploads(player, state);
            }
        }

        private static void UpdatePlayerUploads(Player player,
            HackTimeAuthorityPlayer state) {
            bool hasUploading = false;
            for (int i = 0; i < state.Uploads.Count; i++) {
                AuthorityHackUpload upload = state.Uploads[i];
                QuickHackDef hack = QuickHackDef.GetByIndex(upload.SlotIndex);
                if (hack == null || !TryResolveUploadTarget(upload,
                    out IHackTarget target)) {
                    SendQueueState(player.whoAmI, upload.SessionId,
                        upload.RequestId, upload.SlotIndex, upload.State,
                        upload.Elapsed, upload.UploadFrames, 0,
                        upload.TargetIdentity, accepted: false, player.whoAmI);
                    state.Uploads.RemoveAt(i--);
                    continue;
                }
                upload.Target = target;
                if (upload.State == HackQueueState.Waiting && !hasUploading)
                    upload.State = HackQueueState.Uploading;
                if (upload.State != HackQueueState.Uploading) continue;
                hasUploading = true;
                upload.Elapsed = Math.Min(upload.Elapsed + 1,
                    upload.UploadFrames);
                if (upload.Elapsed % 15 == 0 && Main.netMode == NetmodeID.Server) {
                    SendQueueState(player.whoAmI, upload.SessionId,
                        upload.RequestId, upload.SlotIndex, upload.State,
                        upload.Elapsed, upload.UploadFrames, 0,
                        upload.TargetIdentity, accepted: true, player.whoAmI);
                }
                if (upload.Elapsed < upload.UploadFrames) continue;

                long activationId = AllocateActivationId();
                ActiveHackEffect effect = HackEffectTracker.ApplyAuthorityEffect(
                    hack, target, player.whoAmI, upload.SessionId,
                    upload.RequestId, upload.PaidRamCost, activationId);
                upload.State = HackQueueState.Completed;
                upload.ActivationId = effect?.ActivationId ?? 0;
                SendQueueState(player.whoAmI, upload.SessionId,
                    upload.RequestId, upload.SlotIndex, upload.State,
                    upload.UploadFrames, upload.UploadFrames,
                    upload.ActivationId, upload.TargetIdentity,
                    accepted: effect != null, player.whoAmI);
                state.Uploads.RemoveAt(i--);
                hasUploading = false;
            }
        }

        private static bool TryResolveUploadTarget(AuthorityHackUpload upload,
            out IHackTarget target) {
            target = null;
            if (upload.TargetIdentity.IsSerializable)
                return upload.TargetIdentity.TryResolve(out target);
            if (Main.netMode == NetmodeID.SinglePlayer
                && upload.Target?.IsValid == true) {
                target = upload.Target;
                return true;
            }
            return false;
        }

        internal static long AllocateActivationId() {
            do {
                nextActivationId++;
                if (nextActivationId <= 0) nextActivationId = 1;
            }
            while (HackEffectTracker.FindEffect(nextActivationId) != null);
            return nextActivationId;
        }

        internal static void BroadcastEffectApply(ActiveHackEffect effect,
            int toWho = -1) {
            if (Main.netMode != NetmodeID.Server
                || !TryCreateEffectRecord(effect,
                    out HackEffectSnapshotRecord record)) return;
            ModPacket packet = NewPacket(HackNetOperation.EffectApply);
            WriteEffectRecord(packet, record);
            packet.Send(toWho);
        }

        internal static void BroadcastEffectProgress(ActiveHackEffect effect) {
            if (Main.netMode != NetmodeID.Server || effect == null
                || effect.ActivationId <= 0 || effect.Elapsed < 0
                || effect.Elapsed > HackEffectTracker.MaxEffectDuration) return;
            ModPacket packet = NewPacket(HackNetOperation.EffectProgress);
            packet.Write(effect.ActivationId);
            packet.Write(effect.Elapsed);
            packet.Send();
        }

        internal static void BroadcastEffectRemove(long activationId) {
            if (Main.netMode != NetmodeID.Server || activationId <= 0) return;
            ModPacket packet = NewPacket(HackNetOperation.EffectRemove);
            packet.Write(activationId);
            packet.Send();
        }

        private static void HandleQueueState(BinaryReader reader) {
            bool accepted = reader.ReadBoolean();
            if (!TryReadQueueRecord(reader, out HackQueueSnapshotRecord record)
                || Main.netMode != NetmodeID.MultiplayerClient) return;
            ApplyReplicatedQueueRecord(record, accepted);
        }

        private static void HandleEffectApply(BinaryReader reader) {
            if (!TryReadEffectRecord(reader, out HackEffectSnapshotRecord record)
                || Main.netMode != NetmodeID.MultiplayerClient) return;
            ApplyReplicatedEffectRecord(record);
        }

        private static void HandleEffectProgress(BinaryReader reader) {
            long activationId = reader.ReadInt64();
            int elapsed = reader.ReadInt32();
            if (Main.netMode != NetmodeID.MultiplayerClient || activationId <= 0
                || elapsed < 0 || elapsed > HackEffectTracker.MaxEffectDuration)
                return;
            if (!HackEffectTracker.ApplyReplicatedProgress(activationId, elapsed)) {
                if (pendingProgress.Count < Main.maxNPCs + Main.maxProjectiles)
                    pendingProgress[activationId] = elapsed;
            }
        }

        private static void HandleEffectRemove(BinaryReader reader) {
            long activationId = reader.ReadInt64();
            if (Main.netMode != NetmodeID.MultiplayerClient || activationId <= 0)
                return;
            pendingProgress.Remove(activationId);
            TimeControlReplicationSystem.Cancel<HackEffectPendingSource>(activationId);
            HackEffectTracker.RemoveReplicatedEffect(activationId);
        }

        private static void SendQueueState(int playerIndex, uint sessionId,
            uint requestId, int slotIndex, HackQueueState state, int elapsed,
            int uploadFrames, long activationId, HackNetworkTarget target,
            bool accepted, int toWho) {
            if (Main.netMode != NetmodeID.Server || toWho < 0
                || toWho >= Main.maxPlayers || playerIndex < 0
                || playerIndex >= Main.maxPlayers || sessionId == 0
                || requestId == 0 || slotIndex < 0
                || slotIndex >= QuickHackDef.Count || !target.IsSerializable)
                return;
            var record = new HackQueueSnapshotRecord(playerIndex, sessionId,
                requestId, slotIndex, state, Math.Clamp(elapsed, 0, MaxUploadFrames),
                Math.Clamp(uploadFrames, 1, MaxUploadFrames), activationId, target);
            ModPacket packet = NewPacket(HackNetOperation.QueueState);
            packet.Write(accepted);
            WriteQueueRecord(packet, record);
            packet.Send(toWho);
        }

        private static void ResendRequestState(Player player, uint sessionId,
            uint requestId, int toWho) {
            HackTimeAuthorityPlayer state = player
                .GetModPlayer<HackTimeAuthorityPlayer>();
            for (int i = 0; i < state.Uploads.Count; i++) {
                AuthorityHackUpload upload = state.Uploads[i];
                if (upload.SessionId != sessionId
                    || upload.RequestId != requestId) continue;
                SendQueueState(player.whoAmI, sessionId, requestId,
                    upload.SlotIndex, upload.State, upload.Elapsed,
                    upload.UploadFrames, upload.ActivationId,
                    upload.TargetIdentity, accepted: true, toWho);
                return;
            }
            ActiveHackEffect effect = FindEffectByRequest(player.whoAmI,
                sessionId, requestId);
            if (effect != null) BroadcastEffectApply(effect, toWho);
        }

        private static ActiveHackEffect FindEffectByRequest(int casterIndex,
            uint sessionId, uint requestId) {
            IReadOnlyList<ActiveHackEffect> npcEffects
                = HackEffectTracker.AllActiveEffects;
            for (int i = 0; i < npcEffects.Count; i++) {
                ActiveHackEffect effect = npcEffects[i];
                if (effect.Active && effect.CasterIndex == casterIndex
                    && effect.SessionId == sessionId
                    && effect.RequestId == requestId) return effect;
            }
            IReadOnlyList<ActiveHackEffect> tileEffects
                = HackEffectTracker.AllActiveTileEffects;
            for (int i = 0; i < tileEffects.Count; i++) {
                ActiveHackEffect effect = tileEffects[i];
                if (effect.Active && effect.CasterIndex == casterIndex
                    && effect.SessionId == sessionId
                    && effect.RequestId == requestId) return effect;
            }
            return null;
        }

        internal static bool WriteSnapshot(BinaryWriter writer) {
            if (writer == null || Main.netMode == NetmodeID.MultiplayerClient)
                return false;
            var uploads = new List<HackQueueSnapshotRecord>();
            for (int i = 0; i < Main.maxPlayers
                && uploads.Count < MaxSnapshotUploads; i++) {
                Player player = Main.player[i];
                if (player?.active != true) continue;
                HackTimeAuthorityPlayer state = player
                    .GetModPlayer<HackTimeAuthorityPlayer>();
                for (int j = 0; j < state.Uploads.Count
                    && uploads.Count < MaxSnapshotUploads; j++) {
                    AuthorityHackUpload upload = state.Uploads[j];
                    if (!upload.TargetIdentity.IsSerializable) continue;
                    uploads.Add(new HackQueueSnapshotRecord(i,
                        upload.SessionId, upload.RequestId, upload.SlotIndex,
                        upload.State, upload.Elapsed, upload.UploadFrames,
                        upload.ActivationId, upload.TargetIdentity));
                }
            }

            var effects = new List<HackEffectSnapshotRecord>();
            CollectEffectRecords(HackEffectTracker.AllActiveEffects, effects);
            CollectEffectRecords(HackEffectTracker.AllActiveTileEffects, effects);
            if (effects.Count > MaxSnapshotEffects) return false;

            writer.Write(SnapshotVersion);
            writer.Write((ushort)uploads.Count);
            for (int i = 0; i < uploads.Count; i++)
                WriteQueueRecord(writer, uploads[i]);
            writer.Write((ushort)effects.Count);
            for (int i = 0; i < effects.Count; i++)
                WriteEffectRecord(writer, effects[i]);
            return true;
        }

        internal static bool ReadSnapshot(BinaryReader reader) {
            if (reader == null) return false;
            try {
                if (reader.ReadByte() != SnapshotVersion) return false;
                int uploadCount = reader.ReadUInt16();
                if (uploadCount < 0 || uploadCount > MaxSnapshotUploads)
                    return false;
                var uploads = new List<HackQueueSnapshotRecord>(uploadCount);
                for (int i = 0; i < uploadCount; i++) {
                    if (!TryReadQueueRecord(reader,
                        out HackQueueSnapshotRecord record)) return false;
                    uploads.Add(record);
                }
                int effectCount = reader.ReadUInt16();
                if (effectCount < 0 || effectCount > MaxSnapshotEffects)
                    return false;
                var effects = new List<HackEffectSnapshotRecord>(effectCount);
                for (int i = 0; i < effectCount; i++) {
                    if (!TryReadEffectRecord(reader,
                        out HackEffectSnapshotRecord record)) return false;
                    effects.Add(record);
                }
                if (Main.netMode != NetmodeID.MultiplayerClient) return true;

                HackTimeUI.Instance?.Queue?.Clear();
                HackEffectTracker.BeginReplicatedSnapshot();
                pendingProgress.Clear();
                TimeControlReplicationSystem.CancelAll<HackQueuePendingSource>();
                TimeControlReplicationSystem.CancelAll<HackEffectPendingSource>();
                for (int i = 0; i < uploads.Count; i++)
                    ApplyReplicatedQueueRecord(uploads[i], accepted: true);
                for (int i = 0; i < effects.Count; i++)
                    ApplyReplicatedEffectRecord(effects[i]);
                return true;
            } catch (EndOfStreamException) {
                return false;
            } catch (IOException) {
                return false;
            }
        }

        private static void ApplyReplicatedQueueRecord(
            HackQueueSnapshotRecord record, bool accepted) {
            if (record.PlayerIndex != Main.myPlayer) return;
            HackQueueRenderer queue = HackTimeUI.Instance?.Queue;
            if (queue == null) return;
            if (!accepted) {
                queue.RemoveRequest(record.SessionId, record.RequestId);
                return;
            }
            QuickHackDef hack = QuickHackDef.GetByIndex(record.SlotIndex);
            if (hack == null) return;
            void Apply(IHackTarget target) {
                queue.Enqueue(hack, record.SlotIndex, target, 0,
                    record.SessionId, record.RequestId);
                float progress = record.UploadFrames > 0
                    ? record.Elapsed / (float)record.UploadFrames
                    : 0f;
                queue.ApplyAuthorityState(record.SessionId, record.RequestId,
                    record.SlotIndex, record.State, progress,
                    record.ActivationId, accepted: true);
            }
            if (record.Target.TryResolve(out IHackTarget target)) {
                Apply(target);
                return;
            }
            if (record.Target.Kind != HackTargetKind.Npc) return;
            long pendingId = unchecked(((long)record.SessionId << 32)
                | record.RequestId);
            int remaining = Math.Max(1, record.UploadFrames - record.Elapsed);
            TimeControlReplicationSystem.ResolveOrQueueNPC<HackQueuePendingSource>(
                pendingId, record.Target.NpcIdentity, remaining,
                npc => Apply(new NpcScannable(npc.whoAmI)));
        }

        private static void ApplyReplicatedEffectRecord(
            HackEffectSnapshotRecord record) {
            QuickHackDef hack = QuickHackDef.GetByIndex(record.SlotIndex);
            if (hack == null) return;
            void Apply(IHackTarget target) {
                int elapsed = record.Elapsed;
                if (pendingProgress.Remove(record.ActivationId,
                    out int pendingElapsed)) {
                    elapsed = Math.Max(elapsed, pendingElapsed);
                }
                HackEffectTracker.ApplyReplicatedEffect(record.ActivationId,
                    hack, target, record.Target.NpcIdentity,
                    record.CasterIndex, record.SessionId, record.RequestId,
                    elapsed, record.EffectMult, record.Generation);
                if (record.CasterIndex == Main.myPlayer)
                    HackTimeUI.Instance?.Queue?.RemoveRequest(record.SessionId,
                        record.RequestId);
            }
            if (record.Target.TryResolve(out IHackTarget target)) {
                Apply(target);
                return;
            }
            if (record.Target.Kind != HackTargetKind.Npc) return;
            int duration = Math.Clamp((int)(hack.GetDuration()
                * record.EffectMult), 0, HackEffectTracker.MaxEffectDuration);
            int remaining = Math.Max(1, duration - record.Elapsed);
            TimeControlReplicationSystem.ResolveOrQueueNPC<HackEffectPendingSource>(
                record.ActivationId, record.Target.NpcIdentity, remaining,
                npc => Apply(new NpcScannable(npc.whoAmI)));
        }

        private static void CollectEffectRecords(
            IReadOnlyList<ActiveHackEffect> source,
            List<HackEffectSnapshotRecord> destination) {
            for (int i = 0; i < source.Count
                && destination.Count < MaxSnapshotEffects; i++) {
                ActiveHackEffect effect = source[i];
                if (!effect.Active || !effect.Applied || effect.Replicated
                    || effect.EffectiveDuration <= 0) continue;
                if (TryCreateEffectRecord(effect,
                    out HackEffectSnapshotRecord record)) destination.Add(record);
            }
        }

        private static bool TryCreateEffectRecord(ActiveHackEffect effect,
            out HackEffectSnapshotRecord record) {
            record = default;
            if (effect == null || effect.ActivationId <= 0 || effect.Hack == null
                || effect.Hack.SlotIndex < 0
                || effect.Hack.SlotIndex >= QuickHackDef.Count
                || effect.CasterIndex < 0 || effect.CasterIndex >= Main.maxPlayers
                || effect.Elapsed < 0
                || effect.Elapsed > HackEffectTracker.MaxEffectDuration
                || !float.IsFinite(effect.EffectMult)
                || effect.EffectMult <= 0f || effect.EffectMult > 1f
                || effect.Generation < 0 || effect.Generation > 8) return false;
            HackNetworkTarget target;
            if (effect.Target is TileScannable tileTarget) {
                int tileX = tileTarget.TileCoordX;
                int tileY = tileTarget.TileCoordY;
                if (tileX < 0 || tileX >= Main.maxTilesX
                    || tileY < 0 || tileY >= Main.maxTilesY) return false;
                target = new HackNetworkTarget(HackTargetKind.Tile,
                    default, tileX, tileY);
            }
            else if (!HackNetworkTarget.TryCapture(effect.Target, out target)) {
                return false;
            }
            record = new HackEffectSnapshotRecord(effect.ActivationId,
                effect.CasterIndex, effect.SessionId, effect.RequestId,
                effect.Hack.SlotIndex, effect.Elapsed, effect.EffectMult,
                effect.Generation, target);
            return true;
        }

        private static void WriteQueueRecord(BinaryWriter writer,
            in HackQueueSnapshotRecord record) {
            writer.Write((byte)record.PlayerIndex);
            writer.Write(record.SessionId);
            writer.Write(record.RequestId);
            writer.Write((ushort)record.SlotIndex);
            writer.Write((byte)record.State);
            writer.Write(record.Elapsed);
            writer.Write(record.UploadFrames);
            writer.Write(record.ActivationId);
            WriteTarget(writer, record.Target);
        }

        private static bool TryReadQueueRecord(BinaryReader reader,
            out HackQueueSnapshotRecord record) {
            record = default;
            int playerIndex = reader.ReadByte();
            uint sessionId = reader.ReadUInt32();
            uint requestId = reader.ReadUInt32();
            int slotIndex = reader.ReadUInt16();
            HackQueueState state = (HackQueueState)reader.ReadByte();
            int elapsed = reader.ReadInt32();
            int uploadFrames = reader.ReadInt32();
            long activationId = reader.ReadInt64();
            if (!TryReadTarget(reader, out HackNetworkTarget target)) return false;
            if (!IsValidPlayerIndex(playerIndex) || sessionId == 0
                || requestId == 0 || slotIndex < 0
                || slotIndex >= QuickHackDef.Count
                || (int)state < (int)HackQueueState.Waiting
                || (int)state > (int)HackQueueState.Completed
                || elapsed < 0 || elapsed > MaxUploadFrames
                || uploadFrames <= 0 || uploadFrames > MaxUploadFrames
                || elapsed > uploadFrames || activationId < 0) return false;
            record = new HackQueueSnapshotRecord(playerIndex, sessionId,
                requestId, slotIndex, state, elapsed, uploadFrames,
                activationId, target);
            return true;
        }

        private static void WriteEffectRecord(BinaryWriter writer,
            in HackEffectSnapshotRecord record) {
            writer.Write(record.ActivationId);
            writer.Write((byte)record.CasterIndex);
            writer.Write(record.SessionId);
            writer.Write(record.RequestId);
            writer.Write((ushort)record.SlotIndex);
            writer.Write(record.Elapsed);
            writer.Write(record.EffectMult);
            writer.Write((byte)record.Generation);
            WriteTarget(writer, record.Target);
        }

        private static bool TryReadEffectRecord(BinaryReader reader,
            out HackEffectSnapshotRecord record) {
            record = default;
            long activationId = reader.ReadInt64();
            int casterIndex = reader.ReadByte();
            uint sessionId = reader.ReadUInt32();
            uint requestId = reader.ReadUInt32();
            int slotIndex = reader.ReadUInt16();
            int elapsed = reader.ReadInt32();
            float effectMult = reader.ReadSingle();
            int generation = reader.ReadByte();
            if (!TryReadTarget(reader, out HackNetworkTarget target)) return false;
            if (activationId <= 0 || !IsValidPlayerIndex(casterIndex)
                || slotIndex < 0 || slotIndex >= QuickHackDef.Count
                || elapsed < 0 || elapsed > HackEffectTracker.MaxEffectDuration
                || !float.IsFinite(effectMult) || effectMult <= 0f
                || effectMult > 1f || generation < 0 || generation > 8)
                return false;
            record = new HackEffectSnapshotRecord(activationId, casterIndex,
                sessionId, requestId, slotIndex, elapsed, effectMult,
                generation, target);
            return true;
        }

        private static void WriteTarget(BinaryWriter writer,
            in HackNetworkTarget target) {
            writer.Write((byte)target.Kind);
            if (target.Kind == HackTargetKind.Npc) {
                target.NpcIdentity.Write(writer);
            }
            else if (target.Kind == HackTargetKind.Tile) {
                writer.Write(target.TileX);
                writer.Write(target.TileY);
            }
        }

        private static bool TryReadTarget(BinaryReader reader,
            out HackNetworkTarget target) {
            target = default;
            HackTargetKind kind = (HackTargetKind)reader.ReadByte();
            if (kind == HackTargetKind.Npc) {
                if (!NetworkNPCIdentity.TryRead(reader,
                    out NetworkNPCIdentity identity)) return false;
                target = new HackNetworkTarget(kind, identity, -1, -1);
                return true;
            }
            if (kind == HackTargetKind.Tile) {
                int tileX = reader.ReadInt32();
                int tileY = reader.ReadInt32();
                if (tileX < 0 || tileX >= Main.maxTilesX
                    || tileY < 0 || tileY >= Main.maxTilesY) return false;
                target = new HackNetworkTarget(kind, default, tileX, tileY);
                return true;
            }
            return false;
        }

        private static ModPacket NewPacket(HackNetOperation operation) {
            ModPacket packet = CWRMod.Instance.GetPacket();
            packet.Write((byte)CWRMessageType.HackProtocolApply);
            packet.Write((byte)operation);
            return packet;
        }

        /// <summary>
        /// 仅清理静态同步态。玩家上传队列由 <see cref="HackTimeAuthorityPlayer"/>
        /// 在 Initialize / PlayerDisconnect 中自清，勿在 OnWorldUnload 等时机
        /// 遍历 GetModPlayer（此时 modPlayers 可能已失效）。
        /// </summary>
        internal static void Reset() {
            nextActivationId = 0;
            lastAuthorityUpdateFrame = ulong.MaxValue;
            pendingProgress.Clear();
            TimeControlReplicationSystem.CancelAll<HackQueuePendingSource>();
            TimeControlReplicationSystem.CancelAll<HackEffectPendingSource>();
        }

        private static bool IsValidPlayerIndex(int index)
            => index >= 0 && index < Main.maxPlayers;

        private static bool IsFiniteVector(Vector2 value)
            => float.IsFinite(value.X) && float.IsFinite(value.Y);
    }
}

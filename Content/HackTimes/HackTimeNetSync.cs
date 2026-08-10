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
        //新操作一律追加在末尾，已发布的编号是线上格式的一部分
        OwnedSnapshot,
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
        ProtocolLocked,
    }

    /// <summary>
    /// 目标的跨端身份。新种类一律追加可选参数，别改前四个位置——
    /// 现有构造点全按位置传参
    /// </summary>
    internal readonly record struct HackNetworkTarget(
        HackTargetKind Kind,
        NetworkNPCIdentity NpcIdentity,
        int TileX,
        int TileY,
        NetworkProjectileIdentity ProjIdentity = default,
        int ItemIndex = -1,
        int ItemType = ItemID.None)
    {
        internal bool IsSerializable => Kind switch {
            HackTargetKind.Npc => NpcIdentity.IsValid,
            //液体格与物块格共用同一对座标
            HackTargetKind.Tile or HackTargetKind.Water
                => TileX >= 0 && TileX < Main.maxTilesX
                    && TileY >= 0 && TileY < Main.maxTilesY,
            HackTargetKind.Projectile => ProjIdentity.IsValid,
            HackTargetKind.Item => ItemIndex >= 0 && ItemIndex < Main.maxItems
                && ItemType > ItemID.None && ItemType < ItemLoader.ItemCount,
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
            if (Kind == HackTargetKind.Water) {
                target = new WaterScannable(TileX, TileY);
                return true;
            }
            if (Kind == HackTargetKind.Projectile) {
                //弹幕槽位各端不同，必须靠 owner+identity 反查本机槽
                if (!ProjIdentity.TryResolve(out Projectile projectile)) return false;
                target = new ProjectileScannable(projectile.whoAmI);
                return true;
            }
            if (Kind == HackTargetKind.Item) {
                Item item = Main.item[ItemIndex];
                if (item?.active != true || item.IsAir || item.type != ItemType) {
                    return false;
                }
                target = new ItemScannable(ItemIndex);
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
            if (target is WaterScannable waterTarget) {
                int x = waterTarget.TileCoordX;
                int y = waterTarget.TileCoordY;
                if (x < 0 || x >= Main.maxTilesX || y < 0
                    || y >= Main.maxTilesY || Main.tile[x, y].LiquidAmount == 0) {
                    return false;
                }
                identity = new HackNetworkTarget(HackTargetKind.Water,
                    default, x, y);
                return true;
            }
            if (target is ProjectileScannable projTarget) {
                if (projTarget.ProjectileIndex < 0
                    || projTarget.ProjectileIndex >= Main.maxProjectiles
                    || !NetworkProjectileIdentity.TryCapture(
                        Main.projectile[projTarget.ProjectileIndex],
                        out NetworkProjectileIdentity projIdentity)) {
                    return false;
                }
                identity = new HackNetworkTarget(HackTargetKind.Projectile,
                    default, -1, -1, projIdentity);
                return true;
            }
            if (target is ItemScannable itemTarget) {
                int index = itemTarget.ItemIndex;
                if (index < 0 || index >= Main.maxItems) return false;
                Item item = Main.item[index];
                if (item?.active != true || item.IsAir) return false;
                //掉落物槽位在联机里是全局同步的，index+type 足够认人
                identity = new HackNetworkTarget(HackTargetKind.Item,
                    default, -1, -1, default, index, item.type);
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
                || !HackProtocolOwned.Owns(player, hack)
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
            //本地预扣显示，与服务器同式估算成本；收到回执或超时后对账
            if (!HackTime.InfiniteHack) {
                int predictedCost = Math.Clamp(
                    HackCostEvaluator.GetActualCost(hack, target, player),
                    1, (int)RamSystem.MaxMutationAmount);
                player.GetModPlayer<RAMPlayer>()
                    .RegisterPredictedDebit(token.RequestId, predictedCost);
            }
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
                    case HackNetOperation.OwnedSnapshot:
                        HandleOwnedSnapshot(reader, whoAmI);
                        break;
                }
            } catch (EndOfStreamException) {
            } catch (IOException) {
            }
        }

        #region 协议持有快照

        /// <summary>
        /// 本机持有集上报权威端。持有集归客户端所有，服务端只是拿到校验输入，
        /// 所以这里是单向上行，没有回执也没有下发
        /// </summary>
        internal static void SendOwnedSnapshot(Player player) {
            if (Main.netMode != NetmodeID.MultiplayerClient || player == null
                || player.whoAmI != Main.myPlayer
                || !player.TryGetModPlayer(out HackTimePlayer htp)) {
                return;
            }
            HackProtocolOwned.EnsureSeed(htp);

            List<int> indices = [];
            foreach (string fullName in htp.OwnedProtocols) {
                QuickHackDef hack = QuickHackDef.GetByFullName(fullName);
                if (hack != null && hack.SlotIndex >= 0 && hack.SlotIndex < QuickHackDef.Count) {
                    indices.Add(hack.SlotIndex);
                }
            }

            ModPacket packet = NewPacket(HackNetOperation.OwnedSnapshot);
            packet.Write((ushort)indices.Count);
            for (int i = 0; i < indices.Count; i++) {
                packet.Write((ushort)indices[i]);
            }
            packet.Send();
        }

        private static void HandleOwnedSnapshot(BinaryReader reader, int whoAmI) {
            //先把负载读干净再做守卫：CWRNetWork 让所有 NetHandle 串行共用同一个 reader，
            //提前 return 会把剩余条目留在流里，同包后续分支全部错位
            int count = reader.ReadUInt16();
            List<QuickHackDef> hacks = [];
            for (int i = 0; i < count; i++) {
                int slotIndex = reader.ReadUInt16();
                QuickHackDef hack = QuickHackDef.GetByIndex(slotIndex);
                if (hack != null) {
                    hacks.Add(hack);
                }
            }

            if (Main.netMode != NetmodeID.Server || !IsValidPlayerIndex(whoAmI)) {
                return;
            }
            Player player = Main.player[whoAmI];
            if (player?.active != true) {
                return;
            }
            HackProtocolOwned.ApplyNetworkSnapshot(player, hacks);
        }

        #endregion

        private static void HandleRequest(BinaryReader reader, int whoAmI) {
            uint sessionId = reader.ReadUInt32();
            uint requestId = reader.ReadUInt32();
            int slotIndex = reader.ReadUInt16();
            //先把负载读干净再做守卫：CWRNetWork 让所有 NetHandle 串行共用同一个 reader，
            //目标非法就提前 return 会把这 8 字节留在流里，同包后续分支全部错位
            bool targetValid = TryReadTarget(reader, out HackNetworkTarget identity);
            Vector2 claimedCenter = new(reader.ReadSingle(), reader.ReadSingle());
            if (!targetValid || Main.netMode != NetmodeID.Server
                || !IsValidPlayerIndex(whoAmI))
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
            if (!HasProtocolAuthority(player, hack))
                return HackRequestResultCode.ProtocolLocked;
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

        /// <summary>
        /// 持有校验。服务端在收到该玩家快照前一律放行——进世界时快照与首个请求存在竞态，
        /// 无脑拒会在联机下全员误杀。持有集本就是客户端自报的，这里不是反作弊面
        /// </summary>
        private static bool HasProtocolAuthority(Player player, QuickHackDef hack) {
            if (hack.UnlockedByDefault) {
                return true;
            }
            if (Main.netMode == NetmodeID.Server
                && (!player.TryGetModPlayer(out HackTimePlayer htp)
                    || !htp.OwnedSnapshotReceived)) {
                return true;
            }
            return HackProtocolOwned.Owns(player, hack);
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

        /// <summary>
        /// 目标身份的线上格式：一字节 kind + 该 kind 自己的定长负载。<br/>
        /// 加新 kind 时读写两侧必须同时改，且读侧要先把负载吃干净再校验
        /// </summary>
        private static void WriteTarget(BinaryWriter writer,
            in HackNetworkTarget target) {
            writer.Write((byte)target.Kind);
            if (target.Kind == HackTargetKind.Npc) {
                target.NpcIdentity.Write(writer);
            }
            else if (target.Kind is HackTargetKind.Tile or HackTargetKind.Water) {
                writer.Write(target.TileX);
                writer.Write(target.TileY);
            }
            else if (target.Kind == HackTargetKind.Projectile) {
                target.ProjIdentity.Write(writer);
            }
            else if (target.Kind == HackTargetKind.Item) {
                writer.Write((short)target.ItemIndex);
                writer.Write(target.ItemType);
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
            if (kind is HackTargetKind.Tile or HackTargetKind.Water) {
                int tileX = reader.ReadInt32();
                int tileY = reader.ReadInt32();
                if (tileX < 0 || tileX >= Main.maxTilesX
                    || tileY < 0 || tileY >= Main.maxTilesY) return false;
                target = new HackNetworkTarget(kind, default, tileX, tileY);
                return true;
            }
            if (kind == HackTargetKind.Projectile) {
                if (!NetworkProjectileIdentity.TryRead(reader,
                    out NetworkProjectileIdentity projIdentity)) return false;
                target = new HackNetworkTarget(kind, default, -1, -1, projIdentity);
                return true;
            }
            if (kind == HackTargetKind.Item) {
                //先读满两项再判，提前 return 会把剩下的字节留在共用的流里
                int itemIndex = reader.ReadInt16();
                int itemType = reader.ReadInt32();
                if (itemIndex < 0 || itemIndex >= Main.maxItems
                    || itemType <= ItemID.None
                    || itemType >= ItemLoader.ItemCount) return false;
                target = new HackNetworkTarget(kind, default, -1, -1,
                    default, itemIndex, itemType);
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

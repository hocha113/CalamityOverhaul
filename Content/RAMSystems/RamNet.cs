using System;
using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RAMSystems
{
    internal enum RamUpgradeKind : byte
    {
        Capacity,
        Recovery,
    }

    internal enum RamRequestDisposition : byte
    {
        New,
        Replay,
        Invalid,
        Conflict,
        Expired,
    }

    internal enum RamUpgradeResultCode : byte
    {
        Success,
        InvalidSession,
        ConflictingRequest,
        InvalidPlayer,
        InvalidItem,
        UpgradeLimit,
        InvalidPayload,
        ExpiredRequest,
    }

    internal readonly record struct RamRequestToken(uint SessionId, uint RequestId)
    {
        public bool IsValid => SessionId != 0 && RequestId != 0;
    }

    internal readonly record struct RamRequestResult(
        uint SessionId,
        uint RequestId,
        ushort OperationId,
        byte ResultCode,
        float AppliedAmount,
        uint StateRevision)
    {
        public bool IsValid => SessionId != 0 && RequestId != 0 && OperationId != 0
            && StateRevision != 0 && float.IsFinite(AppliedAmount)
            && MathF.Abs(AppliedAmount) <= RamSystem.MaxMutationAmount;
    }

    internal readonly record struct RamStateSnapshot(
        int PlayerIndex,
        uint SessionId,
        uint Revision,
        int CapacityChips,
        int RecoveryChips,
        int MaxRam,
        float CurrentRam,
        float RecoveryRate,
        float RecoveryCooldown,
        int LockRemain,
        int LockTotal)
    {
        public bool IsValid => PlayerIndex >= 0 && PlayerIndex < Main.maxPlayers
            && SessionId != 0 && Revision != 0
            && CapacityChips >= 0 && CapacityChips <= RamSystem.MaxCapacityUpgradeChips
            && RecoveryChips >= 0 && RecoveryChips <= RamSystem.MaxRecoveryUpgradeChips
            && MaxRam >= RamSystem.MinBaseMaxRam && MaxRam <= RamSystem.SoftMaxBaseMaxRam
            && float.IsFinite(CurrentRam) && CurrentRam >= 0f && CurrentRam <= MaxRam
            && float.IsFinite(RecoveryRate) && RecoveryRate >= 0f
            && RecoveryRate <= RamSystem.MaxEffectiveRecoveryRate
            && float.IsFinite(RecoveryCooldown) && RecoveryCooldown >= 0f
            && RecoveryCooldown <= RamSystem.MaxRecoveryDelay
            && LockRemain >= 0 && LockRemain <= RamSystem.MaxLockFrames
            && LockTotal >= 0 && LockTotal <= RamSystem.MaxLockFrames
            && LockRemain <= LockTotal
            && (LockRemain > 0 || LockTotal == 0);
    }

    /// <summary>RAM 请求总线，类本身即信道（子操作字节继续内部分发）</summary>
    internal class RamNet : CWRNetChannel
    {
        private enum RamNetOp : byte
        {
            InitialProfile,
            StateSnapshot,
            RequestResult,
            UpgradeRequest,
        }

        internal const ushort CapacityUpgradeOperation = 1;
        internal const ushort RecoveryUpgradeOperation = 2;
        internal const ushort FirstExternalOperation = 32;

        private static uint nextSessionId;

        internal static event Action<Player, RamRequestResult> RequestResultReceived;

        internal static uint AllocateSessionId() {
            nextSessionId++;
            if (nextSessionId == 0) {
                nextSessionId = 1;
            }
            return nextSessionId;
        }

        internal static void SendInitialProfile(RAMPlayer state) {
            if (Main.netMode != NetmodeID.MultiplayerClient || state == null
                || state.Player.whoAmI != Main.myPlayer) {
                return;
            }

            ModPacket packet = NewPacket(RamNetOp.InitialProfile);
            packet.Write((byte)Math.Clamp(state.UsedCapacityUpgradeChips,
                0, RamSystem.MaxCapacityUpgradeChips));
            packet.Write((byte)Math.Clamp(state.UsedRecoveryUpgradeChips,
                0, RamSystem.MaxRecoveryUpgradeChips));
            packet.Send();
        }

        internal static bool SendUpgradeRequest(Player player, RamUpgradeKind kind) {
            if (Main.netMode != NetmodeID.MultiplayerClient || player == null
                || !player.active || player.whoAmI != Main.myPlayer
                || kind != RamUpgradeKind.Capacity && kind != RamUpgradeKind.Recovery
                || player.selectedItem < 0 || player.selectedItem >= player.inventory.Length
                //扣除要等回执，未结算期间不再发新请求，否则一张芯片能换到多次升级
                || RamSystem.HasPendingUpgrade(player)
                || !RamSystem.TryAllocateRequest(player, out RamRequestToken token)) {
                return false;
            }

            ModPacket packet = NewPacket(RamNetOp.UpgradeRequest);
            packet.Write(token.SessionId);
            packet.Write(token.RequestId);
            packet.Write((byte)kind);
            packet.Write((byte)player.selectedItem);
            packet.Send();
            player.GetModPlayer<RAMPlayer>()
                .RegisterPendingUpgrade(token.RequestId, player.selectedItem);
            return true;
        }

        internal static void SendStateSnapshot(Player player, int toWho) {
            if (Main.netMode != NetmodeID.Server || player == null || !player.active
                || toWho < 0 || toWho >= Main.maxPlayers) {
                return;
            }
            RAMPlayer state = player.GetModPlayer<RAMPlayer>();
            if (state?.ProfileInitialized != true) {
                return;
            }

            ModPacket packet = NewPacket(RamNetOp.StateSnapshot);
            WriteSnapshot(packet, state.CaptureSnapshot());
            packet.Send(toWho);
            if (toWho == player.whoAmI) {
                state.MarkSnapshotSent();
            }
        }

        internal static void SendRequestResult(Player player, in RamRequestResult result,
            int toWho) {
            if (Main.netMode != NetmodeID.Server || player == null || !player.active
                || toWho < 0 || toWho >= Main.maxPlayers || !result.IsValid) {
                return;
            }
            RAMPlayer state = player.GetModPlayer<RAMPlayer>();
            if (state?.ProfileInitialized != true || state.SessionId != result.SessionId) {
                return;
            }

            ModPacket packet = NewPacket(RamNetOp.RequestResult);
            WriteRequestResult(packet, result);
            WriteSnapshot(packet, state.CaptureSnapshot());
            packet.Send(toWho);
            if (toWho == player.whoAmI) {
                state.MarkSnapshotSent();
            }
        }

        internal static void SendRejectedRequest(Player player, uint sessionId,
            uint requestId, ushort operationId, byte resultCode, int toWho) {
            RAMPlayer state = player?.GetModPlayer<RAMPlayer>();
            if (state?.ProfileInitialized != true || sessionId != state.SessionId
                || requestId == 0 || operationId == 0) {
                SendStateSnapshot(player, toWho);
                return;
            }
            RamRequestResult result = new(state.SessionId, requestId, operationId,
                resultCode, 0f, state.Revision);
            SendRequestResult(player, result, toWho);
        }

        public override void Receive(BinaryReader reader, int whoAmI) {
            try {
                RamNetOp operation = (RamNetOp)reader.ReadByte();
                switch (operation) {
                    case RamNetOp.InitialProfile:
                        HandleInitialProfile(reader, whoAmI);
                        break;
                    case RamNetOp.StateSnapshot:
                        HandleStateSnapshot(reader);
                        break;
                    case RamNetOp.RequestResult:
                        HandleRequestResult(reader);
                        break;
                    case RamNetOp.UpgradeRequest:
                        HandleUpgradeRequest(reader, whoAmI);
                        break;
                }
            } catch (EndOfStreamException) {
            } catch (IOException) {
            }
        }

        internal static void Reset() {
            nextSessionId = 0;
            RequestResultReceived = null;
        }

        private static void HandleInitialProfile(BinaryReader reader, int whoAmI) {
            int capacityChips = Math.Clamp((int)reader.ReadByte(), 0,
                RamSystem.MaxCapacityUpgradeChips);
            int recoveryChips = Math.Clamp((int)reader.ReadByte(), 0,
                RamSystem.MaxRecoveryUpgradeChips);
            if (Main.netMode != NetmodeID.Server
                || !TryResolvePlayer(whoAmI, requireAlive: false, out Player player)) {
                return;
            }

            RAMPlayer state = player.GetModPlayer<RAMPlayer>();
            if (!state.ProfileInitialized) {
                state.InitializeAuthorityProfile(capacityChips, recoveryChips,
                    AllocateSessionId());
            }
            SendStateSnapshot(player, whoAmI);
        }

        private static void HandleStateSnapshot(BinaryReader reader) {
            RamStateSnapshot snapshot = ReadSnapshot(reader);
            if (Main.netMode != NetmodeID.MultiplayerClient || !snapshot.IsValid
                || snapshot.PlayerIndex != Main.myPlayer) {
                return;
            }
            Player player = Main.player[snapshot.PlayerIndex];
            if (player?.active == true) {
                player.GetModPlayer<RAMPlayer>().ApplySnapshot(snapshot);
            }
        }

        private static void HandleRequestResult(BinaryReader reader) {
            RamRequestResult result = ReadRequestResult(reader);
            RamStateSnapshot snapshot = ReadSnapshot(reader);
            if (Main.netMode != NetmodeID.MultiplayerClient || !result.IsValid
                || !snapshot.IsValid || snapshot.PlayerIndex != Main.myPlayer
                || result.SessionId != snapshot.SessionId
                || !IsRevisionAtLeast(snapshot.Revision, result.StateRevision)) {
                return;
            }

            Player player = Main.player[snapshot.PlayerIndex];
            RAMPlayer state = player?.active == true ? player.GetModPlayer<RAMPlayer>() : null;
            if (state == null || !state.ApplySnapshot(snapshot)) {
                return;
            }
            //快照已含该笔扣费的权威结果，撤销本地预扣
            state.SettlePredictedDebit(result.RequestId);
            state.StoreRequestResult(result);
            RequestResultReceived?.Invoke(player, result);
            BaseRamUpgradeChip.HandleRequestResult(player, result);
        }

        private static void HandleUpgradeRequest(BinaryReader reader, int whoAmI) {
            uint sessionId = reader.ReadUInt32();
            uint requestId = reader.ReadUInt32();
            RamUpgradeKind kind = (RamUpgradeKind)reader.ReadByte();
            int inventorySlot = reader.ReadByte();
            if (Main.netMode != NetmodeID.Server
                || !TryResolvePlayer(whoAmI, requireAlive: true, out Player player)) {
                return;
            }

            RAMPlayer state = player.GetModPlayer<RAMPlayer>();
            ushort operationId = GetUpgradeOperation(kind);
            if (operationId == 0 || requestId == 0) {
                SendStateSnapshot(player, whoAmI);
                return;
            }

            RamRequestDisposition disposition = state.ClassifyRequest(sessionId,
                requestId, operationId, out RamRequestResult previous);
            if (disposition == RamRequestDisposition.Replay) {
                SendRequestResult(player, previous, whoAmI);
                return;
            }
            if (disposition == RamRequestDisposition.Invalid) {
                SendStateSnapshot(player, whoAmI);
                return;
            }
            if (disposition != RamRequestDisposition.New) {
                RamUpgradeResultCode failure = disposition == RamRequestDisposition.Conflict
                    ? RamUpgradeResultCode.ConflictingRequest
                    : RamUpgradeResultCode.ExpiredRequest;
                SendRejectedRequest(player, sessionId, requestId, operationId,
                    (byte)failure, whoAmI);
                return;
            }

            RamUpgradeResultCode resultCode = ValidateUpgradeItem(player, inventorySlot, kind);
            float appliedAmount = 0f;
            if (resultCode == RamUpgradeResultCode.Success && !state.CanUseUpgrade(kind)) {
                resultCode = RamUpgradeResultCode.UpgradeLimit;
            }
            if (resultCode == RamUpgradeResultCode.Success) {
                if (!state.TryUseUpgradeAuthority(kind)) {
                    resultCode = RamUpgradeResultCode.UpgradeLimit;
                }
                else {
                    //芯片只校验不扣除：非 ServerSideCharacter 的联机里背包归本机管，
                    //服务端发给玩家自身的槽位同步会被原版丢弃，扣除交给请求方收到回执后执行
                    appliedAmount = kind == RamUpgradeKind.Capacity
                        ? RamSystem.CapacityUpgradeChipBonus
                        : RamSystem.RecoveryUpgradeChipBonus;
                }
            }

            RamRequestToken token = new(sessionId, requestId);
            if (!RamSystem.CompleteRequest(player, token, operationId,
                (byte)resultCode, appliedAmount, out RamRequestResult result)) {
                SendStateSnapshot(player, whoAmI);
                return;
            }
            SendRequestResult(player, result, whoAmI);
        }

        private static RamUpgradeResultCode ValidateUpgradeItem(Player player,
            int inventorySlot, RamUpgradeKind kind) {
            if (kind != RamUpgradeKind.Capacity && kind != RamUpgradeKind.Recovery) {
                return RamUpgradeResultCode.InvalidPayload;
            }
            if (inventorySlot < 0 || inventorySlot >= player.inventory.Length
                || inventorySlot != player.selectedItem) {
                return RamUpgradeResultCode.InvalidItem;
            }
            Item item = player.inventory[inventorySlot];
            int expectedType = GetUpgradeChipType(kind);
            return item != null && !item.IsAir && item.type == expectedType && item.stack > 0
                ? RamUpgradeResultCode.Success
                : RamUpgradeResultCode.InvalidItem;
        }

        private static ushort GetUpgradeOperation(RamUpgradeKind kind) {
            return kind switch {
                RamUpgradeKind.Capacity => CapacityUpgradeOperation,
                RamUpgradeKind.Recovery => RecoveryUpgradeOperation,
                _ => 0,
            };
        }

        /// <summary>芯片类型的唯一映射，权威端校验与本机扣除共用</summary>
        internal static int GetUpgradeChipType(RamUpgradeKind kind) {
            return kind switch {
                RamUpgradeKind.Capacity => ModContent.ItemType<RamCapacityUpgradeChip>(),
                RamUpgradeKind.Recovery => ModContent.ItemType<RamRecoveryUpgradeChip>(),
                _ => ItemID.None,
            };
        }

        internal static bool TryGetUpgradeKind(ushort operationId, out RamUpgradeKind kind) {
            switch (operationId) {
                case CapacityUpgradeOperation:
                    kind = RamUpgradeKind.Capacity;
                    return true;
                case RecoveryUpgradeOperation:
                    kind = RamUpgradeKind.Recovery;
                    return true;
                default:
                    kind = default;
                    return false;
            }
        }

        private static ModPacket NewPacket(RamNetOp operation) {
            ModPacket packet = CWRNetWork.GetPacket<RamNet>();
            packet.Write((byte)operation);
            return packet;
        }

        private static void WriteRequestResult(BinaryWriter writer,
            in RamRequestResult result) {
            writer.Write(result.SessionId);
            writer.Write(result.RequestId);
            writer.Write(result.OperationId);
            writer.Write(result.ResultCode);
            writer.Write(result.AppliedAmount);
            writer.Write(result.StateRevision);
        }

        private static RamRequestResult ReadRequestResult(BinaryReader reader) {
            return new RamRequestResult(
                reader.ReadUInt32(),
                reader.ReadUInt32(),
                reader.ReadUInt16(),
                reader.ReadByte(),
                reader.ReadSingle(),
                reader.ReadUInt32());
        }

        private static void WriteSnapshot(BinaryWriter writer,
            in RamStateSnapshot snapshot) {
            writer.Write((byte)snapshot.PlayerIndex);
            writer.Write(snapshot.SessionId);
            writer.Write(snapshot.Revision);
            writer.Write((byte)snapshot.CapacityChips);
            writer.Write((byte)snapshot.RecoveryChips);
            writer.Write((byte)snapshot.MaxRam);
            writer.Write(snapshot.CurrentRam);
            writer.Write(snapshot.RecoveryRate);
            writer.Write(snapshot.RecoveryCooldown);
            writer.Write(snapshot.LockRemain);
            writer.Write(snapshot.LockTotal);
        }

        private static RamStateSnapshot ReadSnapshot(BinaryReader reader) {
            return new RamStateSnapshot(
                reader.ReadByte(),
                reader.ReadUInt32(),
                reader.ReadUInt32(),
                reader.ReadByte(),
                reader.ReadByte(),
                reader.ReadByte(),
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadInt32(),
                reader.ReadInt32());
        }

        private static bool TryResolvePlayer(int whoAmI, bool requireAlive,
            out Player player) {
            player = null;
            if (whoAmI < 0 || whoAmI >= Main.maxPlayers) {
                return false;
            }
            player = Main.player[whoAmI];
            return player?.active == true && (!requireAlive || !player.dead);
        }

        private static bool IsRevisionAtLeast(uint candidate, uint baseline)
            => candidate == baseline || unchecked((int)(candidate - baseline)) > 0;
    }
}

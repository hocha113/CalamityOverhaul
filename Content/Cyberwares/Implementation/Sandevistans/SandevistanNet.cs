using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Cyberwares.Implementation.Sandevistans
{
    internal readonly record struct SandevistanStateSnapshot(
        int PlayerIndex,
        uint SessionGeneration,
        uint Revision,
        int ItemType,
        bool Active,
        float CurrentCooldown,
        float MaxCooldown,
        float ConsumptionRate,
        float RecoveryRate,
        float SlowFactor,
        int RecoveryDelay)
    {

        internal bool IsValid {
            get {
                if (PlayerIndex < 0 || PlayerIndex >= Main.maxPlayers
                    || SessionGeneration == 0 || Revision == 0
                    || ItemType < ItemID.None || ItemType >= ItemLoader.ItemCount
                    || !float.IsFinite(CurrentCooldown)
                    || !float.IsFinite(MaxCooldown)
                    || !float.IsFinite(ConsumptionRate)
                    || !float.IsFinite(RecoveryRate)
                    || !float.IsFinite(SlowFactor)
                    || SlowFactor < 0.001f || SlowFactor > 1f
                    || RecoveryDelay < 0
                    || RecoveryDelay > SandevistanPlayer.RecoveryDelayTicks) {
                    return false;
                }

                if (ItemType == ItemID.None) {
                    return !Active && CurrentCooldown == 0f
                        && MaxCooldown == 0f && ConsumptionRate == 0f
                        && RecoveryRate == 0f && RecoveryDelay == 0;
                }

                return CurrentCooldown >= 0f
                    && (!Active || CurrentCooldown > 0f)
                    && MaxCooldown >= 0.01f
                    && MaxCooldown <= SandevistanPlayer.MaxCooldownValue
                    && CurrentCooldown <= MaxCooldown
                    && ConsumptionRate >= 0.001f
                    && ConsumptionRate <= SandevistanPlayer.MaxRate
                    && RecoveryRate >= 0f
                    && RecoveryRate <= SandevistanPlayer.MaxRate;
            }
        }
    }

    /// <summary>权威端拒绝开关请求的具体子句，回执给请求方用于反馈与排查</summary>
    internal enum SandevistanDenyReason : byte
    {
        None = 0,
        NotReady,
        Malformed,
        SessionMismatch,
        DuplicateRequest,
        StaleRequest,
        NoEquipment,
        PlayerDead,
        NoCharge,
    }

    /// <summary>三点式请求总线，类本身即信道（子操作字节继续内部分发）</summary>
    internal class SandevistanNet : CWRNetChannel
    {
        private enum NetOperation : byte
        {
            ToggleRequest = 1,
            StateSnapshot = 2,
            AggregateSnapshot = 3,
            ToggleDenied = 4,
        }

        private readonly record struct PendingSnapshot(
            SandevistanStateSnapshot Snapshot,
            ulong ExpiresAt);

        private const ulong PendingLifetimeFrames = 120;
        private static readonly Dictionary<int, PendingSnapshot> pendingSnapshots = [];

        internal static bool SendToggleRequest(SandevistanPlayer state,
            bool desiredActive, uint requestId) {
            if (Main.netMode != NetmodeID.MultiplayerClient || state == null
                || state.Player?.active != true
                || state.Player.whoAmI != Main.myPlayer || requestId == 0
                || state.SessionGeneration == 0 || state.StateRevision == 0) {
                return false;
            }

            ModPacket packet = NewPacket(NetOperation.ToggleRequest);
            packet.Write(state.SessionGeneration);
            packet.Write(requestId);
            packet.Write(state.StateRevision);
            packet.Write(desiredActive);
            packet.Send();
            return true;
        }

        internal static void SendState(SandevistanPlayer state,
            int toWho = -1, int ignoreClient = -1) {
            if (Main.netMode != NetmodeID.Server || state == null
                || state.Player?.active != true || state.SessionGeneration == 0
                || state.StateRevision == 0 || toWho < -1
                || toWho >= Main.maxPlayers || ignoreClient < -1
                || ignoreClient >= Main.maxPlayers) {
                return;
            }

            SandevistanStateSnapshot snapshot = CaptureSnapshot(state);
            if (!snapshot.IsValid) {
                return;
            }
            ModPacket packet = NewPacket(NetOperation.StateSnapshot);
            WriteSnapshot(packet, snapshot);
            packet.Send(toWho, ignoreClient);
        }

        internal static void SendToggleDenied(SandevistanPlayer state, int toWho,
            SandevistanDenyReason reason) {
            if (Main.netMode != NetmodeID.Server || state == null
                || reason == SandevistanDenyReason.None
                || toWho < 0 || toWho >= Main.maxPlayers) {
                return;
            }

            ModPacket packet = NewPacket(NetOperation.ToggleDenied);
            packet.Write((byte)reason);
            packet.Send(toWho);
        }

        internal static void SendAggregate(int toWho = -1,
            int ignoreClient = -1) {
            if (Main.netMode != NetmodeID.Server || toWho < -1
                || toWho >= Main.maxPlayers || ignoreClient < -1
                || ignoreClient >= Main.maxPlayers
                || Sandevistan.AggregateRevision == 0
                || !float.IsFinite(Sandevistan.AggregateTimeScale)
                || Sandevistan.AggregateTimeScale <= 0f
                || Sandevistan.AggregateTimeScale > 1f) {
                return;
            }

            ModPacket packet = NewPacket(NetOperation.AggregateSnapshot);
            packet.Write(Sandevistan.AggregateRevision);
            packet.Write(Sandevistan.AggregateTimeScale);
            packet.Send(toWho, ignoreClient);
        }

        public override void Receive(BinaryReader reader, int whoAmI) {
            try {
                NetOperation operation = (NetOperation)reader.ReadByte();
                switch (operation) {
                    case NetOperation.ToggleRequest:
                        HandleToggleRequest(reader, whoAmI);
                        break;
                    case NetOperation.StateSnapshot:
                        HandleStateSnapshot(reader);
                        break;
                    case NetOperation.AggregateSnapshot:
                        HandleAggregateSnapshot(reader);
                        break;
                    case NetOperation.ToggleDenied:
                        HandleToggleDenied(reader);
                        break;
                    default:
                        //没排空的负载会让同一个 reader 上后续的链式处理器全部错位，
                        //只能在此留证，正常情况下只有版本不一致才会走到这里
                        CWRMod.Instance?.Logger.Warn(
                            $"[Sandevistan] unknown net operation {(byte)operation}, "
                            + "downstream handlers may misread this packet");
                        break;
                }
            } catch (EndOfStreamException ex) {
                CWRMod.Instance?.Logger.Warn(
                    "[Sandevistan] packet underflow: " + ex.Message);
            } catch (IOException ex) {
                CWRMod.Instance?.Logger.Warn(
                    "[Sandevistan] packet read failed: " + ex.Message);
            }
        }

        internal static void UpdatePending() {
            if (Main.netMode != NetmodeID.MultiplayerClient
                || pendingSnapshots.Count == 0) {
                return;
            }

            ulong now = Main.GameUpdateCount;
            int[] playerIndices = [.. pendingSnapshots.Keys];
            foreach (int playerIndex in playerIndices) {
                if (!pendingSnapshots.TryGetValue(playerIndex,
                    out PendingSnapshot pending)) {
                    continue;
                }
                if (now > pending.ExpiresAt) {
                    pendingSnapshots.Remove(playerIndex);
                    continue;
                }
                Player player = Main.player[playerIndex];
                if (player?.active != true) {
                    continue;
                }
                player.GetModPlayer<SandevistanPlayer>().ApplySnapshot(
                    pending.Snapshot.SessionGeneration,
                    pending.Snapshot.Revision,
                    pending.Snapshot.ItemType,
                    pending.Snapshot.Active,
                    pending.Snapshot.CurrentCooldown,
                    pending.Snapshot.MaxCooldown,
                    pending.Snapshot.ConsumptionRate,
                    pending.Snapshot.RecoveryRate,
                    pending.Snapshot.SlowFactor,
                    pending.Snapshot.RecoveryDelay);
                pendingSnapshots.Remove(playerIndex);
            }
        }

        internal static void Reset() {
            pendingSnapshots.Clear();
        }

        private static void HandleToggleRequest(BinaryReader reader, int whoAmI) {
            uint sessionGeneration = reader.ReadUInt32();
            uint requestId = reader.ReadUInt32();
            uint expectedRevision = reader.ReadUInt32();
            bool desiredActive = reader.ReadBoolean();
            if (Main.netMode != NetmodeID.Server
                || !TryResolvePlayer(whoAmI, out Player player)) {
                return;
            }

            SandevistanPlayer state = player.GetModPlayer<SandevistanPlayer>();
            state.HandleAuthorityRequest(sessionGeneration, requestId,
                expectedRevision, desiredActive, whoAmI);
            Sandevistan.ForceAuthorityRecalculation();
        }

        private static void HandleStateSnapshot(BinaryReader reader) {
            SandevistanStateSnapshot snapshot = ReadSnapshot(reader);
            if (Main.netMode != NetmodeID.MultiplayerClient || !snapshot.IsValid) {
                return;
            }

            Player player = Main.player[snapshot.PlayerIndex];
            if (player?.active == true) {
                player.GetModPlayer<SandevistanPlayer>().ApplySnapshot(
                    snapshot.SessionGeneration, snapshot.Revision,
                    snapshot.ItemType, snapshot.Active,
                    snapshot.CurrentCooldown, snapshot.MaxCooldown,
                    snapshot.ConsumptionRate, snapshot.RecoveryRate,
                    snapshot.SlowFactor, snapshot.RecoveryDelay);
                pendingSnapshots.Remove(snapshot.PlayerIndex);
                return;
            }

            if (!pendingSnapshots.TryGetValue(snapshot.PlayerIndex,
                out PendingSnapshot old)
                || IsSnapshotNewer(snapshot, old.Snapshot)) {
                pendingSnapshots[snapshot.PlayerIndex] = new PendingSnapshot(
                    snapshot, Main.GameUpdateCount + PendingLifetimeFrames);
            }
        }

        private static void HandleToggleDenied(BinaryReader reader) {
            SandevistanDenyReason reason = (SandevistanDenyReason)reader.ReadByte();
            if (Main.netMode != NetmodeID.MultiplayerClient
                || reason == SandevistanDenyReason.None) {
                return;
            }

            Player player = Main.LocalPlayer;
            if (player?.active != true) {
                return;
            }
            CWRMod.Instance?.Logger.Info(
                "[Sandevistan] toggle denied by server: " + reason);
            player.GetModPlayer<SandevistanPlayer>().OnToggleDenied();
        }

        private static void HandleAggregateSnapshot(BinaryReader reader) {
            uint revision = reader.ReadUInt32();
            float scale = reader.ReadSingle();
            if (Main.netMode != NetmodeID.MultiplayerClient || revision == 0
                || !float.IsFinite(scale) || scale <= 0f || scale > 1f) {
                return;
            }
            Sandevistan.ApplyReplicatedAggregate(revision, scale);
        }

        private static SandevistanStateSnapshot CaptureSnapshot(
            SandevistanPlayer state)
            => new(state.Player.whoAmI, state.SessionGeneration,
                state.StateRevision, state.EquippedType, state.IsActive,
                state.CurrentCooldown, state.MaxCooldown,
                state.ConsumptionRate, state.RecoveryRate,
                state.SlowFactor, state.RecoveryDelay);

        private static void WriteSnapshot(BinaryWriter writer,
            in SandevistanStateSnapshot snapshot) {
            writer.Write((byte)snapshot.PlayerIndex);
            writer.Write(snapshot.SessionGeneration);
            writer.Write(snapshot.Revision);
            writer.Write(snapshot.ItemType);
            writer.Write(snapshot.Active);
            writer.Write(snapshot.CurrentCooldown);
            writer.Write(snapshot.MaxCooldown);
            writer.Write(snapshot.ConsumptionRate);
            writer.Write(snapshot.RecoveryRate);
            writer.Write(snapshot.SlowFactor);
            writer.Write((ushort)snapshot.RecoveryDelay);
        }

        private static SandevistanStateSnapshot ReadSnapshot(BinaryReader reader)
            => new(reader.ReadByte(), reader.ReadUInt32(), reader.ReadUInt32(),
                reader.ReadInt32(), reader.ReadBoolean(), reader.ReadSingle(),
                reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(),
                reader.ReadSingle(), reader.ReadUInt16());

        private static ModPacket NewPacket(NetOperation operation) {
            ModPacket packet = CWRNetWork.GetPacket<SandevistanNet>();
            packet.Write((byte)operation);
            return packet;
        }

        private static bool TryResolvePlayer(int whoAmI, out Player player) {
            player = null;
            if (whoAmI < 0 || whoAmI >= Main.maxPlayers) {
                return false;
            }
            Player candidate = Main.player[whoAmI];
            if (candidate?.active != true) {
                return false;
            }
            player = candidate;
            return true;
        }

        private static bool IsSnapshotNewer(
            in SandevistanStateSnapshot candidate,
            in SandevistanStateSnapshot baseline) {
            if (candidate.SessionGeneration != baseline.SessionGeneration) {
                return CyberwarePlayer.IsRevisionNewer(
                    candidate.SessionGeneration, baseline.SessionGeneration);
            }
            return candidate.Revision == baseline.Revision
                || CyberwarePlayer.IsRevisionNewer(candidate.Revision,
                    baseline.Revision);
        }
    }
}

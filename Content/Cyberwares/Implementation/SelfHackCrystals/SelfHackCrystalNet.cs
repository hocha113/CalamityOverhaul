using CalamityOverhaul.Content.RAMSystems;
using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Cyberwares.Implementation.SelfHackCrystals
{
    internal enum SelfHackResultCode : byte
    {
        Success,
        InvalidRequest,
        InvalidPlayer,
        MissingCyberware,
        Cooldown,
        InsufficientRam,
        ConflictingRequest,
        ExpiredRequest,
    }

    internal static class SelfHackCrystalNet
    {
        private enum PacketKind : byte
        {
            Request,
            Result,
            State,
        }

        private const ushort RamOperationId = RamNet.FirstExternalOperation + 2;

        internal static bool SendRequest(Player player) {
            if (Main.netMode != NetmodeID.MultiplayerClient
                || player?.active != true || player.whoAmI != Main.myPlayer
                || !RamSystem.TryAllocateRequest(player,
                    out RamRequestToken request)) {
                return false;
            }
            ModPacket packet = NewPacket(PacketKind.Request);
            packet.Write(request.SessionId);
            packet.Write(request.RequestId);
            packet.Send();
            return true;
        }

        internal static void SendState(Player player, int toWho = -1,
            bool playActivation = false) {
            if (Main.netMode != NetmodeID.Server || player?.active != true) {
                return;
            }
            SelfHackCrystalPlayer state = player.GetModPlayer<SelfHackCrystalPlayer>();
            ModPacket packet = NewPacket(PacketKind.State);
            packet.Write((byte)player.whoAmI);
            packet.Write(state.StateRevision);
            packet.Write((ushort)state.SkillCooldownTimer);
            packet.Write(playActivation);
            packet.Send(toWho);
        }

        internal static void NetHandle(CWRMessageType type, BinaryReader reader,
            int whoAmI) {
            if (type != CWRMessageType.SelfHackCrystal || reader == null) {
                return;
            }
            try {
                PacketKind kind = (PacketKind)reader.ReadByte();
                if (Main.netMode == NetmodeID.Server) {
                    if (kind == PacketKind.Request) {
                        HandleRequest(reader, whoAmI);
                    }
                    return;
                }
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    return;
                }
                if (kind == PacketKind.Result) {
                    HandleResult(reader);
                }
                else if (kind == PacketKind.State) {
                    HandleState(reader);
                }
            }
            catch (EndOfStreamException) {
            }
            catch (IOException) {
            }
        }

        private static void HandleRequest(BinaryReader reader, int whoAmI) {
            uint sessionId = reader.ReadUInt32();
            uint requestId = reader.ReadUInt32();
            if (whoAmI < 0 || whoAmI >= Main.maxPlayers || requestId == 0) {
                return;
            }
            Player player = Main.player[whoAmI];
            if (player?.active != true) {
                return;
            }

            RamRequestDisposition disposition = RamSystem.ClassifyRequest(player,
                sessionId, requestId, RamOperationId,
                out RamRequestResult previous);
            if (disposition == RamRequestDisposition.Replay) {
                RamNet.SendRequestResult(player, previous, whoAmI);
                SendResult(player, requestId,
                    (SelfHackResultCode)previous.ResultCode, whoAmI);
                SendState(player, whoAmI);
                return;
            }
            if (disposition == RamRequestDisposition.Invalid) {
                RamNet.SendStateSnapshot(player, whoAmI);
                return;
            }
            if (disposition != RamRequestDisposition.New) {
                SelfHackResultCode code = disposition
                    == RamRequestDisposition.Conflict
                    ? SelfHackResultCode.ConflictingRequest
                    : SelfHackResultCode.ExpiredRequest;
                RamNet.SendRejectedRequest(player, sessionId, requestId,
                    RamOperationId, (byte)code, whoAmI);
                SendResult(player, requestId, code, whoAmI);
                return;
            }

            SelfHackCrystalPlayer state = player.GetModPlayer<SelfHackCrystalPlayer>();
            SelfHackResultCode resultCode = state.TryFireSelfHackAuthority(
                out float paid, out _);
            RamRequestToken request = new(sessionId, requestId);
            if (RamSystem.CompleteRequest(player, request, RamOperationId,
                (byte)resultCode, paid, out RamRequestResult result)) {
                RamNet.SendRequestResult(player, result, whoAmI);
            }
            else {
                RamNet.SendStateSnapshot(player, whoAmI);
            }
            SendResult(player, requestId, resultCode, whoAmI);
            if (resultCode == SelfHackResultCode.Success) {
                SendState(player, playActivation: true);
            }
        }

        private static void SendResult(Player player, uint requestId,
            SelfHackResultCode code, int toWho) {
            if (Main.netMode != NetmodeID.Server || player?.active != true
                || requestId == 0 || toWho < 0 || toWho >= Main.maxPlayers) {
                return;
            }
            RAMPlayer ram = player.GetModPlayer<RAMPlayer>();
            ModPacket packet = NewPacket(PacketKind.Result);
            packet.Write(ram.SessionId);
            packet.Write(requestId);
            packet.Write((byte)code);
            packet.Send(toWho);
        }

        private static void HandleResult(BinaryReader reader) {
            uint sessionId = reader.ReadUInt32();
            uint requestId = reader.ReadUInt32();
            SelfHackResultCode code = (SelfHackResultCode)reader.ReadByte();
            if (sessionId == 0 || sessionId != RamSystem.SessionId
                || requestId == 0 || code > SelfHackResultCode.ExpiredRequest) {
                return;
            }
            if (code != SelfHackResultCode.Success && Main.myPlayer >= 0
                && Main.myPlayer < Main.maxPlayers) {
                Main.player[Main.myPlayer]
                    .GetModPlayer<SelfHackCrystalPlayer>().PlayFailure();
            }
        }

        private static void HandleState(BinaryReader reader) {
            int playerIndex = reader.ReadByte();
            uint revision = reader.ReadUInt32();
            int cooldown = reader.ReadUInt16();
            bool playActivation = reader.ReadBoolean();
            if (playerIndex < 0 || playerIndex >= Main.maxPlayers
                || revision == 0 || cooldown < 0
                || cooldown > SelfHackCrystal.SkillCooldown) {
                return;
            }
            Player player = Main.player[playerIndex];
            if (player?.active != true) {
                return;
            }
            player.GetModPlayer<SelfHackCrystalPlayer>()
                .ApplyReplicatedState(revision, cooldown, playActivation);
        }

        private static ModPacket NewPacket(PacketKind kind) {
            ModPacket packet = CWRMod.Instance.GetPacket();
            packet.Write((byte)CWRMessageType.SelfHackCrystal);
            packet.Write((byte)kind);
            return packet;
        }
    }
}

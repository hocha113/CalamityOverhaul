using CalamityOverhaul.Common;
using CalamityOverhaul.Content.RAMSystems;
using System;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces
{
    internal enum CyberspaceActionKind : byte
    {
        Toggle,
        Activate,
        Deactivate,
        SetLayer,
        Teleport,
        Restart,
    }

    internal enum CyberspaceActionResultCode : byte
    {
        Success,
        InvalidRequest,
        InvalidPlayer,
        InvalidState,
        InvalidPayload,
        InsufficientRam,
        Cooldown,
        ConflictingRequest,
        ExpiredRequest,
    }

    /// <summary>赛博空间动作请求总线，类本身即信道（子操作字节继续内部分发）</summary>
    internal class CyberspaceActionNet : CWRNetChannel
    {
        private enum PacketKind : byte
        {
            Request,
            Result,
            TeleportState,
            RestartState,
            /// <summary>重启回血/清 debuff 归 owner 本机结算</summary>
            RestartRestore,
        }

        private const ushort ToggleOperationId = RamNet.FirstExternalOperation + 3;
        private const ushort ActivateOperationId = RamNet.FirstExternalOperation + 4;
        private const ushort DeactivateOperationId = RamNet.FirstExternalOperation + 5;
        private const ushort SetLayerOperationId = RamNet.FirstExternalOperation + 6;
        internal const ushort TeleportOperationId = RamNet.FirstExternalOperation + 7;
        internal const ushort RestartOperationId = RamNet.FirstExternalOperation + 8;

        internal static bool SendDomainRequest(Player player,
            CyberspaceActionKind action, int layer, Vector2 target) {
            if (action != CyberspaceActionKind.Toggle
                && action != CyberspaceActionKind.Activate
                && action != CyberspaceActionKind.Deactivate
                && action != CyberspaceActionKind.SetLayer) {
                return false;
            }
            return SendRequest(player, action, layer, target);
        }

        internal static bool SendActionRequest(Player player,
            CyberspaceActionKind action, Vector2 target)
            => SendRequest(player, action, 0, target);

        public override void Receive(BinaryReader reader, int whoAmI) {
            if (reader == null) {
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
                if (Main.netMode == NetmodeID.MultiplayerClient) {
                    if (kind == PacketKind.Result) {
                        HandleResult(reader);
                    }
                    else if (kind == PacketKind.TeleportState) {
                        HandleTeleportState(reader);
                    }
                    else if (kind == PacketKind.RestartState) {
                        HandleRestartState(reader);
                    }
                    else if (kind == PacketKind.RestartRestore) {
                        HandleRestartRestore(reader);
                    }
                }
            } catch (EndOfStreamException) {
            } catch (IOException) {
            }
        }

        private static bool SendRequest(Player player,
            CyberspaceActionKind action, int layer, Vector2 target) {
            if (Main.netMode != NetmodeID.MultiplayerClient
                || player?.active != true || player.whoAmI != Main.myPlayer
                || action > CyberspaceActionKind.Restart
                || layer < 0 || layer > Cyberspace.MaxLayerCount
                || !IsValidCoordinate(target)
                || !RamSystem.TryAllocateRequest(player,
                    out RamRequestToken request)) {
                return false;
            }
            ModPacket packet = NewPacket(PacketKind.Request);
            packet.Write(request.SessionId);
            packet.Write(request.RequestId);
            packet.Write((byte)action);
            packet.Write((byte)layer);
            packet.Write(target.X);
            packet.Write(target.Y);
            packet.Send();
            return true;
        }

        private static void HandleRequest(BinaryReader reader, int whoAmI) {
            uint sessionId = reader.ReadUInt32();
            uint requestId = reader.ReadUInt32();
            CyberspaceActionKind action = (CyberspaceActionKind)reader.ReadByte();
            int layer = reader.ReadByte();
            Vector2 target = new(reader.ReadSingle(), reader.ReadSingle());
            if (whoAmI < 0 || whoAmI >= Main.maxPlayers || requestId == 0
                || action > CyberspaceActionKind.Restart
                || layer < 0 || layer > Cyberspace.MaxLayerCount
                || !IsValidCoordinate(target)) {
                return;
            }
            Player player = Main.player[whoAmI];
            if (player?.active != true) {
                return;
            }

            ushort operationId = GetOperationId(action);
            RamRequestDisposition disposition = RamSystem.ClassifyRequest(player,
                sessionId, requestId, operationId,
                out RamRequestResult previous);
            if (disposition == RamRequestDisposition.Replay) {
                RamNet.SendRequestResult(player, previous, whoAmI);
                SendResult(player, requestId, action,
                    (CyberspaceActionResultCode)previous.ResultCode, whoAmI);
                player.GetModPlayer<CyberspacePlayer>()
                    .SendAuthorityState(whoAmI);
                return;
            }
            if (disposition == RamRequestDisposition.Invalid) {
                RamNet.SendStateSnapshot(player, whoAmI);
                return;
            }
            if (disposition != RamRequestDisposition.New) {
                CyberspaceActionResultCode code = disposition
                    == RamRequestDisposition.Conflict
                    ? CyberspaceActionResultCode.ConflictingRequest
                    : CyberspaceActionResultCode.ExpiredRequest;
                RamNet.SendRejectedRequest(player, sessionId, requestId,
                    operationId, (byte)code, whoAmI);
                SendResult(player, requestId, action, code, whoAmI);
                return;
            }

            RamRequestToken request = new(sessionId, requestId);
            CyberspaceActionResultCode resultCode;
            float paid = 0f;
            if (action <= CyberspaceActionKind.SetLayer) {
                resultCode = ExecuteDomainAction(player, action, layer);
            }
            else if (action == CyberspaceActionKind.Teleport) {
                resultCode = Teleport.CyberTeleport.ExecuteAuthority(player,
                    target, out paid);
            }
            else {
                resultCode = Restart.CyberRestart.ExecuteAuthority(player,
                    out paid);
            }

            if (RamSystem.CompleteRequest(player, request, operationId,
                (byte)resultCode, paid, out RamRequestResult result)) {
                RamNet.SendRequestResult(player, result, whoAmI);
            }
            else {
                RamNet.SendStateSnapshot(player, whoAmI);
            }
            SendResult(player, requestId, action, resultCode, whoAmI);
        }

        private static CyberspaceActionResultCode ExecuteDomainAction(
            Player player, CyberspaceActionKind action, int layer) {
            if (!player.Alives()) {
                return CyberspaceActionResultCode.InvalidPlayer;
            }
            CyberspacePlayer state = player.GetModPlayer<CyberspacePlayer>();
            bool deactivating = action == CyberspaceActionKind.Deactivate
                || action == CyberspaceActionKind.Toggle && state.Active;
            if (!deactivating && player.HeldItem.type != SHPCOverride.ID) {
                return CyberspaceActionResultCode.InvalidState;
            }

            switch (action) {
                case CyberspaceActionKind.Toggle:
                    if (state.Active) {
                        state.DeactivateAuthority();
                        return CyberspaceActionResultCode.Success;
                    }
                    if (state.IsCrashLockedOut) {
                        return CyberspaceActionResultCode.Cooldown;
                    }
                    state.ActivateAuthority();
                    return state.Active
                        ? CyberspaceActionResultCode.Success
                        : CyberspaceActionResultCode.InsufficientRam;
                case CyberspaceActionKind.Activate:
                    if (state.Active) {
                        return CyberspaceActionResultCode.Success;
                    }
                    if (state.IsCrashLockedOut) {
                        return CyberspaceActionResultCode.Cooldown;
                    }
                    state.ActivateAuthority();
                    return state.Active
                        ? CyberspaceActionResultCode.Success
                        : CyberspaceActionResultCode.InsufficientRam;
                case CyberspaceActionKind.Deactivate:
                    state.DeactivateAuthority();
                    return CyberspaceActionResultCode.Success;
                case CyberspaceActionKind.SetLayer:
                    if (!state.Active || layer < 1
                        || layer > Cyberspace.MaxLayerCount) {
                        return CyberspaceActionResultCode.InvalidState;
                    }
                    if (layer > state.CurrentLayer
                        && !state.CanAffordLayer(layer)) {
                        return CyberspaceActionResultCode.InsufficientRam;
                    }
                    return state.SetLayerAuthority(layer)
                        ? CyberspaceActionResultCode.Success
                        : CyberspaceActionResultCode.InvalidState;
                default:
                    return CyberspaceActionResultCode.InvalidRequest;
            }
        }

        private static void SendResult(Player player, uint requestId,
            CyberspaceActionKind action, CyberspaceActionResultCode code,
            int toWho) {
            if (Main.netMode != NetmodeID.Server || player?.active != true
                || requestId == 0 || toWho < 0 || toWho >= Main.maxPlayers) {
                return;
            }
            RAMPlayer ram = player.GetModPlayer<RAMPlayer>();
            ModPacket packet = NewPacket(PacketKind.Result);
            packet.Write(ram.SessionId);
            packet.Write(requestId);
            packet.Write((byte)action);
            packet.Write((byte)code);
            packet.Send(toWho);
        }

        internal static void SendTeleportState(Player player, Vector2 origin,
            Vector2 target, bool playVisual, int toWho = -1,
            int ignoreClient = -1) {
            if (Main.netMode != NetmodeID.Server || player?.active != true
                || toWho >= Main.maxPlayers
                || playVisual
                    && (!IsValidCoordinate(origin)
                        || !IsValidCoordinate(target))) {
                return;
            }
            Teleport.CyberTeleportPlayer state =
                player.GetModPlayer<Teleport.CyberTeleportPlayer>();
            ModPacket packet = NewPacket(PacketKind.TeleportState);
            packet.Write((byte)player.whoAmI);
            packet.Write(state.StateRevision);
            packet.Write((ushort)state.CooldownTimer);
            packet.Write((ushort)state.HideTimer);
            packet.Write(playVisual);
            packet.Write(origin.X);
            packet.Write(origin.Y);
            packet.Write(target.X);
            packet.Write(target.Y);
            packet.Send(toWho, ignoreClient);
        }

        private static void HandleResult(BinaryReader reader) {
            uint sessionId = reader.ReadUInt32();
            uint requestId = reader.ReadUInt32();
            CyberspaceActionKind action = (CyberspaceActionKind)reader.ReadByte();
            CyberspaceActionResultCode code =
                (CyberspaceActionResultCode)reader.ReadByte();
            if (sessionId == 0 || sessionId != RamSystem.SessionId
                || requestId == 0 || action > CyberspaceActionKind.Restart
                || code > CyberspaceActionResultCode.ExpiredRequest
                || code == CyberspaceActionResultCode.Success) {
                return;
            }
            RamSystem.NotifyInsufficient();
            if (!Main.dedServ && Main.myPlayer >= 0
                && Main.myPlayer < Main.maxPlayers) {
                Player player = Main.player[Main.myPlayer];
                SoundEngine.PlaySound(CWRSound.FailureCurrent with {
                    Volume = 0.4f,
                    Pitch = -0.35f,
                }, player.Center);
                //拒绝理由只有服务端知道，不回显的话玩家只听见一声响，不知道是缺 RAM
                if (code == CyberspaceActionResultCode.InsufficientRam) {
                    Color denyColor = new(255, 90, 80);
                    CombatText.NewText(player.Hitbox, denyColor, "// LOW RAM", true);
                }
            }
        }

        private static void HandleTeleportState(BinaryReader reader) {
            int playerIndex = reader.ReadByte();
            uint revision = reader.ReadUInt32();
            int cooldown = reader.ReadUInt16();
            int hide = reader.ReadUInt16();
            bool playVisual = reader.ReadBoolean();
            Vector2 origin = new(reader.ReadSingle(), reader.ReadSingle());
            Vector2 target = new(reader.ReadSingle(), reader.ReadSingle());
            if (playerIndex < 0 || playerIndex >= Main.maxPlayers
                || revision == 0 || cooldown < 0
                || cooldown > Teleport.CyberTeleport.CooldownFrames
                || hide < 0 || hide > Teleport.CyberTeleport.HideDuration
                || playVisual
                    && (!IsValidCoordinate(origin)
                        || !IsValidCoordinate(target))) {
                return;
            }
            Player player = Main.player[playerIndex];
            if (player?.active != true) {
                return;
            }
            player.GetModPlayer<Teleport.CyberTeleportPlayer>()
                .ApplyReplicatedState(revision, cooldown, hide, playVisual,
                    origin, target);
        }

        internal static void SendRestartState(Player player,
            Restart.CyberRestart.Runtime state, bool playVisual,
            int toWho = -1) {
            if (Main.netMode != NetmodeID.Server || player?.active != true
                || state == null || toWho >= Main.maxPlayers) {
                return;
            }
            ModPacket packet = NewPacket(PacketKind.RestartState);
            packet.Write((byte)player.whoAmI);
            packet.Write(state.Revision);
            packet.Write((ushort)Math.Clamp(state.ProgressTimer, 0,
                Restart.CyberRestart.TotalFrames));
            packet.Write((byte)Math.Clamp(state.AnchorLayer, 0,
                Cyberspace.MaxLayerCount));
            packet.Write(state.RestoreFired);
            packet.Write(playVisual);
            packet.Send(toWho);
        }

        /// <summary>
        /// 通知施术者本机兑现重启恢复。生命/法力/debuff 归客户端，服务端写了也会被原版覆盖
        /// </summary>
        internal static void SendRestartRestore(Player player) {
            if (Main.netMode != NetmodeID.Server || player?.active != true) {
                return;
            }
            ModPacket packet = NewPacket(PacketKind.RestartRestore);
            packet.Write((byte)player.whoAmI);
            packet.Send(player.whoAmI);
        }

        private static void HandleRestartState(BinaryReader reader) {
            int playerIndex = reader.ReadByte();
            uint revision = reader.ReadUInt32();
            int progress = reader.ReadUInt16();
            int anchorLayer = reader.ReadByte();
            bool restoreFired = reader.ReadBoolean();
            bool playVisual = reader.ReadBoolean();
            if (playerIndex < 0 || playerIndex >= Main.maxPlayers
                || revision == 0 || progress < 0
                || progress > Restart.CyberRestart.TotalFrames
                || anchorLayer < 0 || anchorLayer > Cyberspace.MaxLayerCount) {
                return;
            }
            Player player = Main.player[playerIndex];
            if (player?.active == true) {
                Restart.CyberRestart.ApplyReplicatedState(player, revision,
                    progress, anchorLayer, restoreFired, playVisual);
            }
        }

        private static void HandleRestartRestore(BinaryReader reader) {
            int playerIndex = reader.ReadByte();
            if (playerIndex < 0 || playerIndex >= Main.maxPlayers
                || playerIndex != Main.myPlayer) {
                return;
            }
            Player player = Main.player[playerIndex];
            if (player?.active == true) {
                Restart.CyberRestart.ApplyLocalRestore(player);
            }
        }

        private static ushort GetOperationId(CyberspaceActionKind action) {
            return action switch {
                CyberspaceActionKind.Toggle => ToggleOperationId,
                CyberspaceActionKind.Activate => ActivateOperationId,
                CyberspaceActionKind.Deactivate => DeactivateOperationId,
                CyberspaceActionKind.SetLayer => SetLayerOperationId,
                CyberspaceActionKind.Teleport => TeleportOperationId,
                _ => RestartOperationId,
            };
        }

        private static bool IsValidCoordinate(Vector2 value) {
            if (!float.IsFinite(value.X) || !float.IsFinite(value.Y)) {
                return false;
            }
            float margin = 8192f;
            float maxX = Math.Max(Main.maxTilesX * 16f, 0f) + margin;
            float maxY = Math.Max(Main.maxTilesY * 16f, 0f) + margin;
            return value.X >= -margin && value.X <= maxX
                && value.Y >= -margin && value.Y <= maxY;
        }

        private static ModPacket NewPacket(PacketKind kind) {
            ModPacket packet = CWRNetWork.GetPacket<CyberspaceActionNet>();
            packet.Write((byte)kind);
            return packet;
        }
    }
}

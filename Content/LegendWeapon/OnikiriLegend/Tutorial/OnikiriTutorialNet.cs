using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.Tutorial
{
    internal enum OnikiriTutorialTargetPacket : byte
    {
        Ensure,
        Confirm,
        Release,
    }

    internal sealed class OnikiriTutorialNetPlayer : ModPlayer
    {
        internal int ConfirmedSession;
        internal int ConfirmedTargetIndex = -1;
        internal int RequestedSession;
        internal int EnsureRequestCooldown;
        internal int ServerEnsureCooldown;
        internal int ServerSpawnCooldown;
        internal int ServerSession;
        internal int ServerTargetIndex = -1;

        public override void Initialize() => ResetState();

        public override void OnEnterWorld() => ResetState();

        public override void PostUpdate() {
            if (EnsureRequestCooldown > 0) {
                EnsureRequestCooldown--;
            }
            if (ServerSpawnCooldown > 0) {
                ServerSpawnCooldown--;
            }
            if (ServerEnsureCooldown > 0) {
                ServerEnsureCooldown--;
            }
        }

        internal bool BeginEnsureRequest(int session) {
            if (RequestedSession != session) {
                RequestedSession = session;
                EnsureRequestCooldown = 0;
            }
            if (EnsureRequestCooldown > 0) {
                return false;
            }
            EnsureRequestCooldown = 30;
            return true;
        }

        internal bool BeginServerSpawn(int session) {
            if (ServerSpawnCooldown > 0) {
                return false;
            }
            ServerSpawnCooldown = 30;
            ServerSession = session;
            ServerTargetIndex = -1;
            return true;
        }

        internal bool BeginServerEnsure() {
            if (ServerEnsureCooldown > 0) {
                return false;
            }
            ServerEnsureCooldown = 30;
            return true;
        }

        internal void AcceptConfirm(int session, int npcIndex) {
            RequestedSession = session;
            ConfirmedSession = session;
            ConfirmedTargetIndex = npcIndex;
            EnsureRequestCooldown = npcIndex >= 0 ? 0 : 30;
        }

        internal void ClearClientSession(int session) {
            if (ConfirmedSession == session) {
                ConfirmedSession = 0;
                ConfirmedTargetIndex = -1;
            }
            if (RequestedSession == session) {
                RequestedSession = 0;
                EnsureRequestCooldown = 0;
            }
        }

        internal void ClearServerTarget(int session, int npcIndex) {
            if (ServerSession != session || ServerTargetIndex != npcIndex) {
                return;
            }
            ServerSession = 0;
            ServerTargetIndex = -1;
        }

        private void ResetState() {
            ConfirmedSession = 0;
            ConfirmedTargetIndex = -1;
            RequestedSession = 0;
            EnsureRequestCooldown = 0;
            ServerEnsureCooldown = 0;
            ServerSpawnCooldown = 0;
            ServerSession = 0;
            ServerTargetIndex = -1;
        }
    }

    internal static class OnikiriTutorialNet
    {
        internal static void RequestEnsureTarget(int session) {
            if (Main.dedServ || Main.gameMenu || session == 0) {
                return;
            }
            Player player = Main.LocalPlayer;
            if (player?.active != true || player.dead) {
                return;
            }
            OnikiriTutorialNetPlayer state = player.GetModPlayer<OnikiriTutorialNetPlayer>();
            if (GetLocalTarget(player.whoAmI, session) != null || !state.BeginEnsureRequest(session)) {
                return;
            }

            if (Main.netMode == NetmodeID.SinglePlayer) {
                EnsureServerTarget(player, session);
                return;
            }

            ModPacket packet = CWRMod.Instance.GetPacket();
            packet.Write((byte)CWRMessageType.OnikiriTutorialTarget);
            packet.Write((byte)OnikiriTutorialTargetPacket.Ensure);
            packet.Write(session);
            packet.Send();
        }

        internal static void RequestReleaseTarget(int session) {
            if (Main.dedServ || session == 0) {
                return;
            }
            Player player = Main.LocalPlayer;
            if (player == null) {
                return;
            }
            NPC target = GetLocalTarget(player.whoAmI, session);
            if (target != null) {
                OnikiriTutorialTargetGlobal.ReleasePresentation(target);
            }
            player.GetModPlayer<OnikiriTutorialNetPlayer>().ClearClientSession(session);

            if (Main.netMode == NetmodeID.SinglePlayer) {
                OnikiriTutorialTargetGlobal.ReleaseTargets(player.whoAmI, session);
                return;
            }
            if (Main.netMode != NetmodeID.MultiplayerClient) {
                return;
            }

            ModPacket packet = CWRMod.Instance.GetPacket();
            packet.Write((byte)CWRMessageType.OnikiriTutorialTarget);
            packet.Write((byte)OnikiriTutorialTargetPacket.Release);
            packet.Write(session);
            packet.Send();
        }

        internal static NPC GetLocalTarget(int owner, int session) {
            if (owner < 0 || owner >= Main.maxPlayers || session == 0) {
                return null;
            }

            if (owner == Main.myPlayer && Main.myPlayer >= 0 && Main.myPlayer < Main.maxPlayers) {
                OnikiriTutorialNetPlayer state = Main.player[owner].GetModPlayer<OnikiriTutorialNetPlayer>();
                if (state.ConfirmedSession == session
                    && TryGetMatchingTarget(state.ConfirmedTargetIndex, owner, session, out NPC confirmed)) {
                    return confirmed;
                }
            }

            foreach (NPC npc in Main.ActiveNPCs) {
                if (!OnikiriTutorialTargetGlobal.IsTutorialTarget(npc, out int targetOwner, out int targetSession)
                    || targetOwner != owner || targetSession != session) {
                    continue;
                }
                if (owner == Main.myPlayer) {
                    Main.player[owner].GetModPlayer<OnikiriTutorialNetPlayer>()
                        .AcceptConfirm(session, npc.whoAmI);
                }
                return npc;
            }
            return null;
        }

        internal static void NetHandle(CWRMessageType type, BinaryReader reader, int whoAmI) {
            if (type != CWRMessageType.OnikiriTutorialTarget) {
                return;
            }

            OnikiriTutorialTargetPacket operation = (OnikiriTutorialTargetPacket)reader.ReadByte();
            switch (operation) {
                case OnikiriTutorialTargetPacket.Ensure:
                    ReceiveEnsure(reader, whoAmI);
                    break;
                case OnikiriTutorialTargetPacket.Confirm:
                    ReceiveConfirm(reader);
                    break;
                case OnikiriTutorialTargetPacket.Release:
                    ReceiveRelease(reader, whoAmI);
                    break;
            }
        }

        private static void ReceiveEnsure(BinaryReader reader, int whoAmI) {
            int session = reader.ReadInt32();
            if (Main.netMode != NetmodeID.Server || session <= 0
                || whoAmI < 0 || whoAmI >= Main.maxPlayers) {
                return;
            }
            Player player = Main.player[whoAmI];
            if (player.GetModPlayer<OnikiriTutorialNetPlayer>().BeginServerEnsure()) {
                EnsureServerTarget(player, session);
            }
        }

        private static void ReceiveConfirm(BinaryReader reader) {
            int owner = reader.ReadByte();
            int session = reader.ReadInt32();
            int npcIndex = reader.ReadInt16();
            if (Main.netMode != NetmodeID.MultiplayerClient || owner != Main.myPlayer
                || owner < 0 || owner >= Main.maxPlayers) {
                return;
            }
            Main.player[owner].GetModPlayer<OnikiriTutorialNetPlayer>()
                .AcceptConfirm(session, npcIndex);
        }

        private static void ReceiveRelease(BinaryReader reader, int whoAmI) {
            if (Main.netMode == NetmodeID.Server) {
                int session = reader.ReadInt32();
                if (session <= 0 || whoAmI < 0 || whoAmI >= Main.maxPlayers) {
                    return;
                }
                OnikiriTutorialNetPlayer state = Main.player[whoAmI]
                    .GetModPlayer<OnikiriTutorialNetPlayer>();
                if (state.ServerSession != session) {
                    return;
                }
                int targetIndex = state.ServerTargetIndex;
                if (targetIndex < 0
                    || !OnikiriTutorialTargetGlobal.ReleaseTarget(whoAmI, session, targetIndex)) {
                    state.ClearServerTarget(session, targetIndex);
                }
                return;
            }

            int owner = reader.ReadByte();
            int releasedSession = reader.ReadInt32();
            if (Main.netMode == NetmodeID.MultiplayerClient && owner == Main.myPlayer
                && owner >= 0 && owner < Main.maxPlayers) {
                Main.player[owner].GetModPlayer<OnikiriTutorialNetPlayer>()
                    .ClearClientSession(releasedSession);
            }
        }

        private static void EnsureServerTarget(Player player, int session) {
            if (Main.netMode == NetmodeID.MultiplayerClient || player?.active != true
                || player.dead || session <= 0 || !player.HasItem(OnikiriOverride.ID)) {
                return;
            }

            NPC existing = OnikiriTutorialTargetGlobal.FindTarget(player.whoAmI, session);
            if (existing != null) {
                RegisterServerTarget(player, session, existing.whoAmI);
                SendConfirm(player.whoAmI, session, existing.whoAmI);
                return;
            }

            OnikiriTutorialNetPlayer state = player.GetModPlayer<OnikiriTutorialNetPlayer>();
            if (!state.BeginServerSpawn(session)) {
                SendConfirm(player.whoAmI, session, -1);
                return;
            }

            OnikiriTutorialTargetGlobal.ReleaseTargets(player.whoAmI, session: null);
            NPC target = OnikiriTutorialTargetGlobal.SpawnTarget(player, session);
            int targetIndex = target?.whoAmI ?? -1;
            RegisterServerTarget(player, session, targetIndex);
            SendConfirm(player.whoAmI, session, targetIndex);
        }

        private static void RegisterServerTarget(Player player, int session, int npcIndex) {
            OnikiriTutorialNetPlayer state = player.GetModPlayer<OnikiriTutorialNetPlayer>();
            state.ServerSession = session;
            state.ServerTargetIndex = npcIndex;
        }

        private static void SendConfirm(int owner, int session, int npcIndex) {
            if (owner < 0 || owner >= Main.maxPlayers) {
                return;
            }
            if (Main.netMode == NetmodeID.SinglePlayer) {
                Main.player[owner].GetModPlayer<OnikiriTutorialNetPlayer>()
                    .AcceptConfirm(session, npcIndex);
                return;
            }
            if (Main.netMode != NetmodeID.Server || !Main.player[owner].active) {
                return;
            }

            ModPacket packet = CWRMod.Instance.GetPacket();
            packet.Write((byte)CWRMessageType.OnikiriTutorialTarget);
            packet.Write((byte)OnikiriTutorialTargetPacket.Confirm);
            packet.Write((byte)owner);
            packet.Write(session);
            packet.Write((short)npcIndex);
            packet.Send(owner);
        }

        internal static void NotifyTargetReleased(int owner, int session, int npcIndex) {
            if (owner < 0 || owner >= Main.maxPlayers) {
                return;
            }
            OnikiriTutorialNetPlayer state = Main.player[owner].GetModPlayer<OnikiriTutorialNetPlayer>();
            state.ClearServerTarget(session, npcIndex);

            if (Main.netMode == NetmodeID.SinglePlayer) {
                state.ClearClientSession(session);
                return;
            }
            if (Main.netMode != NetmodeID.Server || !Main.player[owner].active) {
                return;
            }

            ModPacket packet = CWRMod.Instance.GetPacket();
            packet.Write((byte)CWRMessageType.OnikiriTutorialTarget);
            packet.Write((byte)OnikiriTutorialTargetPacket.Release);
            packet.Write((byte)owner);
            packet.Write(session);
            packet.Send(owner);
        }

        private static bool TryGetMatchingTarget(int npcIndex, int owner, int session, out NPC target) {
            target = null;
            if (npcIndex < 0 || npcIndex >= Main.maxNPCs) {
                return false;
            }
            NPC npc = Main.npc[npcIndex];
            if (!OnikiriTutorialTargetGlobal.IsTutorialTarget(npc, out int targetOwner, out int targetSession)
                || targetOwner != owner || targetSession != session) {
                return false;
            }
            target = npc;
            return true;
        }
    }
}

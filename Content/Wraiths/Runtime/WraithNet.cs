using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend;
using CalamityOverhaul.Content.Players;
using CalamityOverhaul.Content.Wraiths.Core;
using CalamityOverhaul.Content.Wraiths.Projectiles;
using CalamityOverhaul.Content.Wraiths.VFX;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Wraiths.Runtime
{
    internal enum WraithNetOp : byte
    {
        ReservedHaltRequest = 0,
        ReservedRiteRequest = 1,
        ReservedRiteConfirm = 2,
        ReservedBacklashSpawn = 3,
        ScapeGhostFx = 4,
        RuleKill = 5,
        ReservedOmenStart = 6,
        ReservedOmenCancel = 7,
        ReservedSiteSync = 8,
        ReservedScapeStateSync = 9,
        InitialState = 10,
        StateSync = 11,
        EquipRequest = 12,
        EquipResult = 13,
        HeadlessImpactRequest = 14,
        GhostHandGripRequest = 15,
    }

    internal enum WraithEquipResult : byte
    {
        Success,
        InvalidPlayer,
        InvalidItem,
        IdentityMismatch,
        DuplicateIdentity,
        StaleRevision,
        InvalidWraith,
        RateLimited,
        SessionNotReady,
    }

    /// <summary>玩家役鬼状态、资源事件与替死演出的权威网络通道。</summary>
    internal static class WraithNet
    {
        private const ushort NoWraith = ushort.MaxValue;
        private const int MaxPendingEquipRequests = 16;
        private const ulong PendingLifetimeTicks = 600;
        private const ulong RequestWindowTicks = 60;
        private const int MaxRequestsPerWindow = 12;

        private sealed class PendingEquipRequest
        {
            internal ushort RequestId;
            internal uint SessionToken;
            internal byte InventorySlot;
            internal long InstanceId;
            internal uint ExpectedRevision;
            internal ushort RequestedWraithId;
            internal ulong CreatedAt;
            internal Action<bool> Completion;
        }

        private struct RequestWindow
        {
            internal ulong StartedAt;
            internal int Count;
        }

        private static readonly Dictionary<ushort, PendingEquipRequest> pendingEquip = [];
        private static readonly Dictionary<int, RequestWindow> equipRequestWindows = [];
        private static ushort nextRequestId;
        private static uint nextSessionToken;

        internal static void ClearSession() {
            List<PendingEquipRequest> abandoned = [.. pendingEquip.Values];
            pendingEquip.Clear();
            equipRequestWindows.Clear();
            nextRequestId = 0;
            nextSessionToken = 0;
            foreach (PendingEquipRequest request in abandoned) {
                CompletePending(request, false);
            }
        }

        internal static void UpdatePending(Player player) {
            if (Main.netMode == NetmodeID.MultiplayerClient
                && player?.whoAmI == Main.myPlayer) {
                SweepPending();
            }
        }

        private static ModPacket NewPacket(WraithNetOp op) {
            ModPacket packet = CWRMod.Instance.GetPacket();
            packet.Write((byte)CWRMessageType.Wraith);
            packet.Write((byte)op);
            return packet;
        }

        public static bool RequestEquippedWraith(Player player, Item sourceItem, string key,
            Action<bool> completed = null) {
            WraithPlayer state = player?.GetModPlayer<WraithPlayer>();
            OnikiriData data = OnikiriData.TryGet(sourceItem);
            if (player == null || player.whoAmI != Main.myPlayer || state == null
                || !state.SessionInitialized || data == null
                || !TryResolveSelectedSword(player, sourceItem, out byte inventorySlot)
                || OnikiriNet.HasDuplicateInstanceId(player, data.InstanceId)) {
                return false;
            }

            ushort wraithId = NoWraith;
            string normalizedKey = string.IsNullOrEmpty(key) ? string.Empty : key;
            if (!string.IsNullOrEmpty(normalizedKey)
                && (!WraithRegistry.TryGetUsable(normalizedKey, out _)
                    || !WraithRegistry.TryGetNetworkId(normalizedKey, out wraithId))) {
                return false;
            }
            if (state.EquippedWraithKey == normalizedKey) {
                return false;
            }

            if (Main.netMode != NetmodeID.MultiplayerClient) {
                bool success = state.TrySetEquippedAuthority(normalizedKey, state.LoadoutRevision);
                completed?.Invoke(success);
                return success;
            }

            if (!TryTrackEquip(inventorySlot, data.InstanceId, state.LoadoutRevision,
                wraithId, completed, out PendingEquipRequest pending)) {
                return false;
            }

            ModPacket packet = NewPacket(WraithNetOp.EquipRequest);
            packet.Write(pending.RequestId);
            packet.Write(pending.SessionToken);
            packet.Write(pending.InventorySlot);
            packet.Write(pending.InstanceId);
            packet.Write(pending.ExpectedRevision);
            packet.Write(pending.RequestedWraithId);
            packet.Send();
            return true;
        }

        internal static void SendInitialState(WraithPlayer state) {
            if (Main.netMode != NetmodeID.MultiplayerClient || state?.Player == null
                || state.Player.whoAmI != Main.myPlayer) {
                return;
            }
            ClearLocalPending();
            ModPacket packet = NewPacket(WraithNetOp.InitialState);
            WriteSavedState(packet, state);
            packet.Send();
        }

        internal static void SendStateSync(int playerWhoAmI, int toWho = -1) {
            if (Main.netMode != NetmodeID.Server || playerWhoAmI < 0
                || playerWhoAmI >= Main.maxPlayers) {
                return;
            }
            Player player = Main.player[playerWhoAmI];
            WraithPlayer state = player?.GetModPlayer<WraithPlayer>();
            if (player?.active != true || state == null || !state.SessionInitialized) {
                return;
            }
            ModPacket packet = NewPacket(WraithNetOp.StateSync);
            packet.Write((byte)playerWhoAmI);
            WriteStateSync(packet, state);
            packet.Send(toWho);
        }

        public static void RequestHeadlessImpact(Projectile projectile, ushort serial,
            int targetId, int targetType, Vector2 impact) {
            if (projectile?.active != true
                || projectile.ModProjectile is not HeadlessShadeProj shade
                || projectile.owner < 0 || projectile.owner >= Main.maxPlayers) {
                return;
            }
            if (Main.netMode != NetmodeID.MultiplayerClient) {
                shade.TryApplyAuthorityImpact(serial, targetId, targetType, impact);
                return;
            }
            if (projectile.owner != Main.myPlayer) {
                return;
            }
            ModPacket packet = NewPacket(WraithNetOp.HeadlessImpactRequest);
            packet.Write(projectile.identity);
            packet.Write(serial);
            packet.Write(targetId);
            packet.Write(targetType);
            packet.WriteVector2(impact);
            packet.Send();
        }

        public static void RequestGhostHandGrip(Projectile projectile, ushort serial,
            int targetId, int targetType) {
            if (projectile?.active != true
                || projectile.ModProjectile is not GhostHandProj hand
                || projectile.owner < 0 || projectile.owner >= Main.maxPlayers) {
                return;
            }
            if (Main.netMode != NetmodeID.MultiplayerClient) {
                hand.TryApplyAuthorityGrip(serial, targetId, targetType);
                return;
            }
            if (projectile.owner != Main.myPlayer) {
                return;
            }
            ModPacket packet = NewPacket(WraithNetOp.GhostHandGripRequest);
            packet.Write(projectile.identity);
            packet.Write(serial);
            packet.Write(targetId);
            packet.Write(targetType);
            packet.Send();
        }

        internal static void SendScapeGhostFx(Vector2 from, Vector2 to, int victimWhoAmI,
            string targetName = null, bool revivalKilled = false) {
            if (Main.netMode != NetmodeID.Server || victimWhoAmI < 0
                || victimWhoAmI >= Main.maxPlayers) {
                return;
            }
            ModPacket packet = NewPacket(WraithNetOp.ScapeGhostFx);
            packet.WriteVector2(from);
            packet.WriteVector2(to);
            packet.Write((byte)victimWhoAmI);
            packet.Write(targetName ?? string.Empty);
            packet.Write(revivalKilled);
            packet.Send();
        }

        internal static void SendRuleKill(int playerWhoAmI, WraithDefinition definition,
            string reasonKey = null) {
            if (Main.netMode != NetmodeID.Server || definition == null
                || playerWhoAmI < 0 || playerWhoAmI >= Main.maxPlayers) {
                return;
            }
            ModPacket packet = NewPacket(WraithNetOp.RuleKill);
            packet.Write((byte)playerWhoAmI);
            packet.Write(definition.Key);
            packet.Write(reasonKey ?? string.Empty);
            packet.Send();
        }

        public static void NetHandle(CWRMessageType type, BinaryReader reader, int whoAmI) {
            if (type != CWRMessageType.Wraith) {
                return;
            }
            WraithNetOp op = (WraithNetOp)reader.ReadByte();
            if (Main.netMode == NetmodeID.Server) {
                switch (op) {
                    case WraithNetOp.InitialState:
                        HandleInitialState(reader, whoAmI);
                        break;
                    case WraithNetOp.EquipRequest:
                        HandleEquipRequest(reader, whoAmI);
                        break;
                    case WraithNetOp.HeadlessImpactRequest:
                        HandleHeadlessImpact(reader, whoAmI);
                        break;
                    case WraithNetOp.GhostHandGripRequest:
                        HandleGhostHandGrip(reader, whoAmI);
                        break;
                }
                return;
            }
            if (Main.netMode != NetmodeID.MultiplayerClient) {
                return;
            }
            switch (op) {
                case WraithNetOp.ScapeGhostFx:
                    HandleScapeGhostFx(reader);
                    break;
                case WraithNetOp.RuleKill:
                    HandleRuleKill(reader);
                    break;
                case WraithNetOp.StateSync:
                    HandleStateSync(reader);
                    break;
                case WraithNetOp.EquipResult:
                    HandleEquipResult(reader);
                    break;
            }
        }

        private static void HandleInitialState(BinaryReader reader, int whoAmI) {
            ReadSavedState(reader, out string equipped,
                out float scapeMastery, out bool scapeDormant,
                out float shadeMastery, out bool shadeDormant,
                out float handMastery, out bool handDormant,
                out float erosion, out float revival, out int multiplier,
                out int erosionIdle, out int revivalIdle);
            Player player = ResolvePlayer(whoAmI, requireAlive: false);
            WraithPlayer state = player?.GetModPlayer<WraithPlayer>();
            if (state == null || !state.AcceptInitialState(equipped,
                scapeMastery, scapeDormant, shadeMastery, shadeDormant,
                handMastery, handDormant, erosion, revival, multiplier,
                erosionIdle, revivalIdle)) {
                return;
            }
            SendStateSync(whoAmI);
        }

        private static void HandleStateSync(BinaryReader reader) {
            int playerIndex = reader.ReadByte();
            ReadStateSync(reader, out string equipped, out uint loadoutRevision,
                out uint resourceRevision,
                out float scapeMastery, out bool scapeDormant,
                out float shadeMastery, out bool shadeDormant,
                out float handMastery, out bool handDormant,
                out float erosion, out float revival, out int multiplier,
                out int erosionIdle, out int revivalIdle);
            if (playerIndex < 0 || playerIndex >= Main.maxPlayers) {
                return;
            }
            Player player = Main.player[playerIndex];
            WraithPlayer state = player?.GetModPlayer<WraithPlayer>();
            if (player?.active != true || state == null) {
                return;
            }
            state.ApplyNetworkState(equipped, loadoutRevision, resourceRevision,
                scapeMastery, scapeDormant, shadeMastery, shadeDormant,
                handMastery, handDormant, erosion, revival, multiplier,
                erosionIdle, revivalIdle, force: !state.SessionInitialized);
        }

        private static void HandleEquipRequest(BinaryReader reader, int whoAmI) {
            ushort requestId = reader.ReadUInt16();
            uint sessionToken = reader.ReadUInt32();
            byte inventorySlot = reader.ReadByte();
            long instanceId = reader.ReadInt64();
            uint expectedRevision = reader.ReadUInt32();
            ushort requestedWraithId = reader.ReadUInt16();

            Player player = ResolvePlayer(whoAmI, requireAlive: true);
            WraithPlayer state = player?.GetModPlayer<WraithPlayer>();
            WraithEquipResult result = ValidateEquipSource(player, state, inventorySlot,
                instanceId, expectedRevision, whoAmI);
            string requestedKey = string.Empty;
            if (result == WraithEquipResult.Success && requestedWraithId != NoWraith) {
                if (!WraithRegistry.TryGetByNetworkId(requestedWraithId,
                    out WraithDefinition definition) || !definition.CanEquip) {
                    result = WraithEquipResult.InvalidWraith;
                }
                else {
                    requestedKey = definition.Key;
                }
            }
            if (result == WraithEquipResult.Success
                && !state.TrySetEquippedAuthority(requestedKey, expectedRevision)) {
                result = WraithEquipResult.StaleRevision;
            }

            SendStateSync(whoAmI, whoAmI);
            ModPacket packet = NewPacket(WraithNetOp.EquipResult);
            packet.Write(requestId);
            packet.Write(sessionToken);
            packet.Write((byte)result);
            packet.Write(inventorySlot);
            packet.Write(instanceId);
            packet.Write(state?.LoadoutRevision ?? expectedRevision);
            packet.Write(GetWraithNetworkId(state?.EquippedWraithKey));
            packet.Send(whoAmI);
        }

        private static WraithEquipResult ValidateEquipSource(Player player, WraithPlayer state,
            byte inventorySlot, long instanceId, uint expectedRevision, int whoAmI) {
            if (player == null || state == null) {
                return WraithEquipResult.InvalidPlayer;
            }
            if (!state.SessionInitialized) {
                return WraithEquipResult.SessionNotReady;
            }
            if (!AllowEquipRequest(whoAmI)) {
                return WraithEquipResult.RateLimited;
            }
            if (inventorySlot != player.selectedItem || inventorySlot >= player.inventory.Length) {
                return WraithEquipResult.InvalidItem;
            }
            Item item = player.inventory[inventorySlot];
            OnikiriData data = OnikiriData.TryGet(item);
            if (item == null || item.IsAir || item.type != OnikiriOverride.ID || data == null) {
                return WraithEquipResult.InvalidItem;
            }
            if (instanceId == 0 || data.InstanceId != instanceId) {
                return WraithEquipResult.IdentityMismatch;
            }
            if (OnikiriNet.HasDuplicateInstanceId(player, instanceId)) {
                return WraithEquipResult.DuplicateIdentity;
            }
            return state.LoadoutRevision == expectedRevision
                ? WraithEquipResult.Success : WraithEquipResult.StaleRevision;
        }

        private static void HandleEquipResult(BinaryReader reader) {
            ushort requestId = reader.ReadUInt16();
            uint sessionToken = reader.ReadUInt32();
            WraithEquipResult result = (WraithEquipResult)reader.ReadByte();
            byte inventorySlot = reader.ReadByte();
            long instanceId = reader.ReadInt64();
            uint revision = reader.ReadUInt32();
            ushort equippedWraithId = reader.ReadUInt16();
            PendingEquipRequest pending = TakePending(requestId);
            if (pending == null) {
                return;
            }

            bool valid = pending.SessionToken == sessionToken
                && pending.InventorySlot == inventorySlot
                && pending.InstanceId == instanceId
                && result <= WraithEquipResult.SessionNotReady
                && revision >= pending.ExpectedRevision;
            if (valid && result == WraithEquipResult.Success) {
                valid = equippedWraithId == pending.RequestedWraithId;
            }
            CompletePending(pending, valid && result == WraithEquipResult.Success);
        }

        private static void HandleHeadlessImpact(BinaryReader reader, int whoAmI) {
            int projectileIdentity = reader.ReadInt32();
            ushort serial = reader.ReadUInt16();
            int targetId = reader.ReadInt32();
            int targetType = reader.ReadInt32();
            Vector2 impact = reader.ReadVector2();
            Player owner = ResolvePlayer(whoAmI, requireAlive: true);
            if (owner == null || !float.IsFinite(impact.X) || !float.IsFinite(impact.Y)) {
                return;
            }

            Projectile projectile = ResolveOwnedProjectile(whoAmI, projectileIdentity,
                ModContent.ProjectileType<HeadlessShadeProj>());
            if (projectile?.ModProjectile is not HeadlessShadeProj shade) {
                return;
            }
            shade.TryApplyAuthorityImpact(serial, targetId, targetType, impact);
        }

        private static void HandleGhostHandGrip(BinaryReader reader, int whoAmI) {
            int projectileIdentity = reader.ReadInt32();
            ushort serial = reader.ReadUInt16();
            int targetId = reader.ReadInt32();
            int targetType = reader.ReadInt32();
            Player owner = ResolvePlayer(whoAmI, requireAlive: true);
            if (owner == null) {
                return;
            }

            Projectile projectile = ResolveOwnedProjectile(whoAmI, projectileIdentity,
                ModContent.ProjectileType<GhostHandProj>());
            if (projectile?.ModProjectile is not GhostHandProj hand) {
                return;
            }
            hand.TryApplyAuthorityGrip(serial, targetId, targetType);
        }

        private static void HandleScapeGhostFx(BinaryReader reader) {
            Vector2 from = reader.ReadVector2();
            Vector2 to = reader.ReadVector2();
            int victim = reader.ReadByte();
            string targetName = reader.ReadString();
            bool revivalKilled = reader.ReadBoolean();
            if (victim < 0 || victim >= Main.maxPlayers) {
                return;
            }
            if (victim != Main.myPlayer) {
                ScapeArmRenderer.Trigger(from, to);
                return;
            }
            Main.LocalPlayer.TryGetOverride(out PlayerDeath playerDeath);
            if (playerDeath != null) {
                playerDeath.ApplyScapeSuccess(from, to, targetName, revivalKilled);
                return;
            }
            ScapeArmRenderer.Trigger(from, to);
            string name = string.IsNullOrWhiteSpace(targetName)
                ? WraithSystemText.ScapeGhostUnknownTarget.Value : targetName;
            VaultUtils.Text(WraithSystemText.ScapeGhostActivated.Format(name),
                new Color(178, 34, 44));
        }

        private static void HandleRuleKill(BinaryReader reader) {
            int victim = reader.ReadByte();
            string key = reader.ReadString();
            _ = reader.ReadString();
            if (victim < 0 || victim >= Main.maxPlayers
                || !WraithRegistry.TryGet(key, out _)) {
                return;
            }
            Main.player[victim].TryGetOverride(out PlayerDeath playerDeath);
            playerDeath?.PrepareRuleDeath();
        }

        private static void WriteSavedState(BinaryWriter writer, WraithPlayer state) {
            state.ExportSnapshot(out string equipped, out _, out _,
                out float scapeMastery, out bool scapeDormant,
                out float shadeMastery, out bool shadeDormant,
                out float handMastery, out bool handDormant,
                out float erosion, out float revival, out int multiplier,
                out int erosionIdle, out int revivalIdle);
            writer.Write(GetWraithNetworkId(equipped));
            WriteResources(writer, scapeMastery, scapeDormant,
                shadeMastery, shadeDormant, handMastery, handDormant,
                erosion, revival, multiplier, erosionIdle, revivalIdle);
        }

        private static void ReadSavedState(BinaryReader reader, out string equipped,
            out float scapeMastery, out bool scapeDormant,
            out float shadeMastery, out bool shadeDormant,
            out float handMastery, out bool handDormant,
            out float erosion, out float revival, out int multiplier,
            out int erosionIdle, out int revivalIdle) {
            equipped = ResolveUsableKey(reader.ReadUInt16());
            ReadResources(reader, out scapeMastery, out scapeDormant,
                out shadeMastery, out shadeDormant, out handMastery, out handDormant,
                out erosion, out revival, out multiplier, out erosionIdle, out revivalIdle);
        }

        private static void WriteStateSync(BinaryWriter writer, WraithPlayer state) {
            state.ExportSnapshot(out string equipped, out uint loadoutRevision,
                out uint resourceRevision,
                out float scapeMastery, out bool scapeDormant,
                out float shadeMastery, out bool shadeDormant,
                out float handMastery, out bool handDormant,
                out float erosion, out float revival, out int multiplier,
                out int erosionIdle, out int revivalIdle);
            writer.Write(GetWraithNetworkId(equipped));
            writer.Write(loadoutRevision);
            writer.Write(resourceRevision);
            WriteResources(writer, scapeMastery, scapeDormant,
                shadeMastery, shadeDormant, handMastery, handDormant,
                erosion, revival, multiplier, erosionIdle, revivalIdle);
        }

        private static void ReadStateSync(BinaryReader reader, out string equipped,
            out uint loadoutRevision, out uint resourceRevision,
            out float scapeMastery, out bool scapeDormant,
            out float shadeMastery, out bool shadeDormant,
            out float handMastery, out bool handDormant,
            out float erosion, out float revival, out int multiplier,
            out int erosionIdle, out int revivalIdle) {
            equipped = ResolveUsableKey(reader.ReadUInt16());
            loadoutRevision = reader.ReadUInt32();
            resourceRevision = reader.ReadUInt32();
            ReadResources(reader, out scapeMastery, out scapeDormant,
                out shadeMastery, out shadeDormant, out handMastery, out handDormant,
                out erosion, out revival, out multiplier, out erosionIdle, out revivalIdle);
        }

        private static void WriteResources(BinaryWriter writer,
            float scapeMastery, bool scapeDormant,
            float shadeMastery, bool shadeDormant,
            float handMastery, bool handDormant,
            float erosion, float revival, int multiplier,
            int erosionIdle, int revivalIdle) {
            writer.Write(scapeMastery);
            writer.Write(scapeDormant);
            writer.Write(shadeMastery);
            writer.Write(shadeDormant);
            writer.Write(handMastery);
            writer.Write(handDormant);
            writer.Write(erosion);
            writer.Write(revival);
            writer.Write((byte)WraithPlayer.SanitizeScapeMultiplier(multiplier));
            writer.Write(erosionIdle);
            writer.Write(revivalIdle);
        }

        private static void ReadResources(BinaryReader reader,
            out float scapeMastery, out bool scapeDormant,
            out float shadeMastery, out bool shadeDormant,
            out float handMastery, out bool handDormant,
            out float erosion, out float revival, out int multiplier,
            out int erosionIdle, out int revivalIdle) {
            scapeMastery = reader.ReadSingle();
            scapeDormant = reader.ReadBoolean();
            shadeMastery = reader.ReadSingle();
            shadeDormant = reader.ReadBoolean();
            handMastery = reader.ReadSingle();
            handDormant = reader.ReadBoolean();
            erosion = reader.ReadSingle();
            revival = reader.ReadSingle();
            multiplier = reader.ReadByte();
            erosionIdle = reader.ReadInt32();
            revivalIdle = reader.ReadInt32();
        }

        private static ushort GetWraithNetworkId(string key) {
            if (!string.IsNullOrEmpty(key)
                && WraithRegistry.TryGetNetworkId(key, out ushort id)) {
                return id;
            }
            return NoWraith;
        }

        private static string ResolveUsableKey(ushort id) {
            if (id != NoWraith && WraithRegistry.TryGetByNetworkId(id,
                out WraithDefinition definition) && definition.CanEquip) {
                return definition.Key;
            }
            return string.Empty;
        }

        private static bool TryResolveSelectedSword(Player player, Item source,
            out byte inventorySlot) {
            inventorySlot = 0;
            int selected = player?.selectedItem ?? -1;
            if (selected < 0 || selected >= player.inventory.Length
                || selected > byte.MaxValue || !ReferenceEquals(player.inventory[selected], source)
                || source.type != OnikiriOverride.ID || !ReferenceEquals(player.HeldItem, source)) {
                return false;
            }
            inventorySlot = (byte)selected;
            return true;
        }

        private static bool TryTrackEquip(byte inventorySlot, long instanceId,
            uint expectedRevision, ushort requestedWraithId, Action<bool> completed,
            out PendingEquipRequest request) {
            SweepPending();
            request = null;
            if (pendingEquip.Count >= MaxPendingEquipRequests) {
                return false;
            }
            foreach (PendingEquipRequest active in pendingEquip.Values) {
                if (active.InstanceId == instanceId) {
                    return false;
                }
            }
            ushort requestId;
            do {
                requestId = ++nextRequestId;
            }
            while (requestId == 0 || pendingEquip.ContainsKey(requestId));
            uint sessionToken = ++nextSessionToken;
            if (sessionToken == 0) {
                sessionToken = ++nextSessionToken;
            }
            request = new PendingEquipRequest {
                RequestId = requestId,
                SessionToken = sessionToken,
                InventorySlot = inventorySlot,
                InstanceId = instanceId,
                ExpectedRevision = expectedRevision,
                RequestedWraithId = requestedWraithId,
                CreatedAt = Main.GameUpdateCount,
                Completion = completed,
            };
            pendingEquip.Add(requestId, request);
            return true;
        }

        private static PendingEquipRequest TakePending(ushort requestId) {
            SweepPending();
            return pendingEquip.Remove(requestId, out PendingEquipRequest request)
                ? request : null;
        }

        private static void SweepPending() {
            if (pendingEquip.Count == 0) {
                return;
            }
            ulong now = Main.GameUpdateCount;
            List<PendingEquipRequest> expired = [];
            foreach (ushort id in new List<ushort>(pendingEquip.Keys)) {
                if (now - pendingEquip[id].CreatedAt > PendingLifetimeTicks) {
                    expired.Add(pendingEquip[id]);
                    pendingEquip.Remove(id);
                }
            }
            foreach (PendingEquipRequest request in expired) {
                CompletePending(request, false);
            }
        }

        private static void ClearLocalPending() {
            List<PendingEquipRequest> abandoned = [.. pendingEquip.Values];
            pendingEquip.Clear();
            nextRequestId = 0;
            nextSessionToken = 0;
            foreach (PendingEquipRequest request in abandoned) {
                CompletePending(request, false);
            }
        }

        private static void CompletePending(PendingEquipRequest request, bool success) {
            Action<bool> completion = request?.Completion;
            if (request != null) {
                request.Completion = null;
            }
            completion?.Invoke(success);
        }

        private static bool AllowEquipRequest(int whoAmI) {
            ulong now = Main.GameUpdateCount;
            if (!equipRequestWindows.TryGetValue(whoAmI, out RequestWindow window)
                || now - window.StartedAt >= RequestWindowTicks) {
                equipRequestWindows[whoAmI] = new RequestWindow {
                    StartedAt = now,
                    Count = 1,
                };
                return true;
            }
            if (window.Count >= MaxRequestsPerWindow) {
                return false;
            }
            window.Count++;
            equipRequestWindows[whoAmI] = window;
            return true;
        }

        private static Projectile ResolveOwnedProjectile(int owner, int identity, int expectedType) {
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile projectile = Main.projectile[i];
                if (projectile.active && projectile.owner == owner
                    && projectile.identity == identity && projectile.type == expectedType) {
                    return projectile;
                }
            }
            return null;
        }

        private static Player ResolvePlayer(int whoAmI, bool requireAlive) {
            if (whoAmI < 0 || whoAmI >= Main.maxPlayers) {
                return null;
            }
            Player player = Main.player[whoAmI];
            if (player?.active != true || requireAlive && player.dead) {
                return null;
            }
            return player;
        }
    }

    internal sealed class WraithNetSessionSystem : ModSystem
    {
        public override void OnWorldLoad() => WraithNet.ClearSession();

        public override void OnWorldUnload() => WraithNet.ClearSession();
    }
}

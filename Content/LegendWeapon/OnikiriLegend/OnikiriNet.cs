using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.Inscriptions;
using CalamityOverhaul.Content.Wraiths.Core;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend
{
    internal enum OnikiriNetOp : byte
    {
        OwnedMeiSnapshot,
        MeiRequest,
        MeiResult,
        AttuneRequest,
        AttuneResult,
    }

    internal enum OnikiriNetResult : byte
    {
        Success,
        InvalidItem,
        IdentityMismatch,
        StaleRevision,
        InvalidDefinition,
        NotOwned,
        NotEligible,
        RateLimited,
    }

    /// <summary>鬼切铭刻与共鸣的字段级权威同步</summary>
    internal static class OnikiriNet
    {
        private const ushort NoDefinition = ushort.MaxValue;
        private const int MaxPendingOperations = 64;
        private const ulong PendingLifetimeTicks = 600;
        private const ulong RequestWindowTicks = 60;
        private const int MaxRequestsPerWindow = 12;
        private const ulong AuthorityLifetimeTicks = 600;

        private sealed class PendingOperation
        {
            public OnikiriNetOp Operation;
            public byte InventorySlot;
            public byte Field;
            public long InstanceId;
            public uint ExpectedRevision;
            public ulong CreatedAt;
            public Action<bool> Completion;
        }

        private struct RequestWindow
        {
            public ulong StartedAt;
            public int Count;
        }

        private sealed class AuthoritativeEditState
        {
            public readonly WraithProgressStore Wraiths = new();
            public readonly OniMeiStore Mei = new();
            public uint Revision;
            public ulong CapturedAt;

            public void Capture(OnikiriData data) {
                Wraiths.CopyFrom(data.Wraiths);
                Mei.CopyFrom(data.Mei);
                Revision = data.EditRevision;
                CapturedAt = Main.GameUpdateCount;
            }
        }

        private static readonly Dictionary<ushort, PendingOperation> pending = [];
        private static readonly Dictionary<int, RequestWindow> requestWindows = [];
        private static readonly Dictionary<(int Player, long InstanceId), AuthoritativeEditState>
            authoritativeEdits = [];
        private static ushort nextRequestId;

        public static void NetHandle(CWRMessageType type, BinaryReader reader, int whoAmI) {
            if (type != CWRMessageType.OnikiriItemOperation) {
                return;
            }

            OnikiriNetOp op = (OnikiriNetOp)reader.ReadByte();
            if (VaultUtils.isServer) {
                switch (op) {
                    case OnikiriNetOp.OwnedMeiSnapshot:
                        ReceiveOwnedMeiSnapshot(reader, whoAmI);
                        break;
                    case OnikiriNetOp.MeiRequest:
                        ReceiveMeiRequest(reader, whoAmI);
                        break;
                    case OnikiriNetOp.AttuneRequest:
                        ReceiveAttuneRequest(reader, whoAmI);
                        break;
                }
                return;
            }

            if (!VaultUtils.isClient) {
                return;
            }
            switch (op) {
                case OnikiriNetOp.MeiResult:
                    ReceiveMeiResult(reader);
                    break;
                case OnikiriNetOp.AttuneResult:
                    ReceiveAttuneResult(reader);
                    break;
            }
        }

        public static bool TryChangeMei(Player player, Item item, OniMeiSlotKind slot, string key,
            Action<bool> completed = null) {
            OnikiriData data = OnikiriData.TryGet(item);
            if (player == null || player.whoAmI != Main.myPlayer || data == null
                || slot < OniMeiSlotKind.Nakago || slot > OniMeiSlotKind.Horimono
                || !TryResolveLocalSlot(player, item, out byte inventorySlot)) {
                return false;
            }

            ushort definitionId = NoDefinition;
            if (key != null) {
                if (!OniMeiOwned.Owns(player, key)
                    || !OniMeiRegistry.TryGet(key, out OniMeiDefinition definition)
                    || definition.SlotKind != slot
                    || !OniMeiRegistry.TryGetNetworkId(key, out definitionId)) {
                    return false;
                }
            }
            if (data.Mei.Get(slot) == key) {
                return false;
            }

            if (!VaultUtils.isClient) {
                if (!ApplyMeiState(item, slot, key)) {
                    return false;
                }
                data.AdvanceEditRevision();
                completed?.Invoke(true);
                return true;
            }

            if (!TryTrackPending(OnikiriNetOp.MeiRequest, inventorySlot, (byte)slot,
                data.InstanceId, data.EditRevision, completed, out ushort requestId)) {
                return false;
            }
            ModPacket packet = NewPacket(OnikiriNetOp.MeiRequest);
            packet.Write(requestId);
            packet.Write(inventorySlot);
            packet.Write(data.InstanceId);
            packet.Write(data.EditRevision);
            packet.Write((byte)slot);
            packet.Write(definitionId);
            packet.Send();
            return true;
        }

        public static bool TryAttune(Player player, Item item, string key,
            Action<bool> completed = null) {
            OnikiriData data = OnikiriData.TryGet(item);
            if (player == null || player.whoAmI != Main.myPlayer || data == null
                || !WraithRegistry.TryGet(key, out WraithDefinition definition)
                || !WraithRegistry.TryGetNetworkId(key, out ushort definitionId)
                || !CanAttune(data.Wraiths, definition)
                || data.Wraiths.AttunedKey == key
                || !TryResolveLocalSlot(player, item, out byte inventorySlot)) {
                return false;
            }

            if (!VaultUtils.isClient) {
                if (!data.Wraiths.TryAttune(key)) {
                    return false;
                }
                data.AdvanceEditRevision();
                completed?.Invoke(true);
                return true;
            }

            if (!TryTrackPending(OnikiriNetOp.AttuneRequest, inventorySlot, 0,
                data.InstanceId, data.EditRevision, completed, out ushort requestId)) {
                return false;
            }
            ModPacket packet = NewPacket(OnikiriNetOp.AttuneRequest);
            packet.Write(requestId);
            packet.Write(inventorySlot);
            packet.Write(data.InstanceId);
            packet.Write(data.EditRevision);
            packet.Write(definitionId);
            packet.Send();
            return true;
        }

        public static void SendOwnedMeiSnapshot(Player player) {
            if (!VaultUtils.isClient || player == null || player.whoAmI != Main.myPlayer
                || !player.TryGetModPlayer(out OnikiriPlayer onikiri)) {
                return;
            }
            OniMeiOwned.EnsureSeed(onikiri);
            HashSet<ushort> uniqueIds = [];
            foreach (string key in onikiri.OwnedMeiKeys) {
                if (OniMeiRegistry.TryGetNetworkId(key, out ushort id)) {
                    uniqueIds.Add(id);
                }
            }
            List<ushort> ids = [.. uniqueIds];
            ids.Sort();

            ModPacket packet = NewPacket(OnikiriNetOp.OwnedMeiSnapshot);
            packet.Write((ushort)ids.Count);
            foreach (ushort id in ids) {
                packet.Write(id);
            }
            packet.Send();
        }

        private static void ReceiveOwnedMeiSnapshot(BinaryReader reader, int whoAmI) {
            Player player = ResolveSender(whoAmI);
            int count = reader.ReadUInt16();
            if (player == null || count > OniMeiRegistry.All.Count) {
                return;
            }
            List<string> keys = new(count);
            for (int i = 0; i < count; i++) {
                if (OniMeiRegistry.TryGetByNetworkId(reader.ReadUInt16(), out OniMeiDefinition definition)) {
                    keys.Add(definition.Key);
                }
            }
            OniMeiOwned.ApplyNetworkSnapshot(player, keys);
        }

        private static void ReceiveMeiRequest(BinaryReader reader, int whoAmI) {
            ushort requestId = reader.ReadUInt16();
            byte inventorySlot = reader.ReadByte();
            long requestedInstanceId = reader.ReadInt64();
            uint expectedRevision = reader.ReadUInt32();
            byte rawSlot = reader.ReadByte();
            ushort definitionId = reader.ReadUInt16();

            Player player = ResolveSender(whoAmI);
            Item item = ResolveServerTarget(player, inventorySlot);
            OnikiriData data = OnikiriData.TryGet(item);
            OnikiriNetResult result = ValidateTarget(player, data, requestedInstanceId,
                expectedRevision, whoAmI);

            if (result == OnikiriNetResult.Success) {
                if (rawSlot > (byte)OniMeiSlotKind.Horimono) {
                    result = OnikiriNetResult.InvalidDefinition;
                }
                else {
                    OniMeiSlotKind slot = (OniMeiSlotKind)rawSlot;
                    string key = null;
                    if (definitionId != NoDefinition) {
                        if (!OniMeiRegistry.TryGetByNetworkId(definitionId, out OniMeiDefinition definition)
                            || definition.SlotKind != slot) {
                            result = OnikiriNetResult.InvalidDefinition;
                        }
                        else if (!OniMeiOwned.Owns(player, definition.Key)) {
                            result = OnikiriNetResult.NotOwned;
                        }
                        else {
                            key = definition.Key;
                        }
                    }
                    if (result == OnikiriNetResult.Success && data.Mei.Get(slot) != key) {
                        if (!ApplyMeiState(item, slot, key)) {
                            result = OnikiriNetResult.InvalidDefinition;
                        }
                        else {
                            data.AdvanceEditRevision();
                        }
                    }
                }
            }

            string currentKey = data != null && data.InstanceId == requestedInstanceId
                && rawSlot <= (byte)OniMeiSlotKind.Horimono
                ? data.Mei.Get((OniMeiSlotKind)rawSlot) : null;
            if (result == OnikiriNetResult.Success) {
                RecordAuthoritativeState(player, data);
            }
            SendMeiResult(whoAmI, requestId, result, inventorySlot, data,
                requestedInstanceId, expectedRevision, rawSlot, currentKey);
        }

        private static void ReceiveMeiResult(BinaryReader reader) {
            ushort requestId = reader.ReadUInt16();
            OnikiriNetResult result = (OnikiriNetResult)reader.ReadByte();
            byte inventorySlot = reader.ReadByte();
            long instanceId = reader.ReadInt64();
            uint authoritativeRevision = reader.ReadUInt32();
            byte rawSlot = reader.ReadByte();
            ushort definitionId = reader.ReadUInt16();
            PendingOperation operation = TakePending(requestId);
            if (operation == null) {
                return;
            }
            if (operation.Operation != OnikiriNetOp.MeiRequest
                || operation.InventorySlot != inventorySlot
                || operation.Field != rawSlot || rawSlot > (byte)OniMeiSlotKind.Horimono
                || result > OnikiriNetResult.RateLimited
                || authoritativeRevision < operation.ExpectedRevision
                || result is OnikiriNetResult.InvalidItem or OnikiriNetResult.IdentityMismatch) {
                CompletePending(operation, false);
                return;
            }
            if (operation.InstanceId != instanceId) {
                CompletePending(operation, false);
                return;
            }

            string key = null;
            if (definitionId != NoDefinition) {
                if (!OniMeiRegistry.TryGetByNetworkId(definitionId, out OniMeiDefinition definition)
                    || definition.SlotKind != (OniMeiSlotKind)rawSlot) {
                    CompletePending(operation, false);
                    return;
                }
                key = definition.Key;
            }
            bool applied = ApplyAuthoritativeMei(Main.LocalPlayer, inventorySlot, instanceId,
                authoritativeRevision, (OniMeiSlotKind)rawSlot, key);
            CompletePending(operation, result == OnikiriNetResult.Success && applied);
        }

        private static void ReceiveAttuneRequest(BinaryReader reader, int whoAmI) {
            ushort requestId = reader.ReadUInt16();
            byte inventorySlot = reader.ReadByte();
            long requestedInstanceId = reader.ReadInt64();
            uint expectedRevision = reader.ReadUInt32();
            ushort definitionId = reader.ReadUInt16();

            Player player = ResolveSender(whoAmI);
            Item item = ResolveServerTarget(player, inventorySlot);
            OnikiriData data = OnikiriData.TryGet(item);
            OnikiriNetResult result = ValidateTarget(player, data, requestedInstanceId,
                expectedRevision, whoAmI);
            if (result == OnikiriNetResult.Success) {
                if (!WraithRegistry.TryGetByNetworkId(definitionId, out WraithDefinition definition)) {
                    result = OnikiriNetResult.InvalidDefinition;
                }
                else if (!CanAttune(data.Wraiths, definition)) {
                    result = OnikiriNetResult.NotEligible;
                }
                else if (data.Wraiths.AttunedKey != definition.Key) {
                    if (!data.Wraiths.TryAttune(definition.Key)) {
                        result = OnikiriNetResult.NotEligible;
                    }
                    else {
                        data.AdvanceEditRevision();
                    }
                }
            }
            string currentKey = data != null && data.InstanceId == requestedInstanceId
                ? data.Wraiths.AttunedKey : null;
            if (result == OnikiriNetResult.Success) {
                RecordAuthoritativeState(player, data);
            }
            SendAttuneResult(whoAmI, requestId, result, inventorySlot, data,
                requestedInstanceId, expectedRevision, currentKey);
        }

        private static void ReceiveAttuneResult(BinaryReader reader) {
            ushort requestId = reader.ReadUInt16();
            OnikiriNetResult result = (OnikiriNetResult)reader.ReadByte();
            byte inventorySlot = reader.ReadByte();
            long instanceId = reader.ReadInt64();
            uint authoritativeRevision = reader.ReadUInt32();
            ushort definitionId = reader.ReadUInt16();
            PendingOperation operation = TakePending(requestId);
            if (operation == null) {
                return;
            }
            if (operation.Operation != OnikiriNetOp.AttuneRequest
                || operation.InventorySlot != inventorySlot
                || result > OnikiriNetResult.RateLimited
                || authoritativeRevision < operation.ExpectedRevision
                || result is OnikiriNetResult.InvalidItem or OnikiriNetResult.IdentityMismatch) {
                CompletePending(operation, false);
                return;
            }
            if (operation.InstanceId != instanceId) {
                CompletePending(operation, false);
                return;
            }

            string key = null;
            if (definitionId != NoDefinition) {
                if (!WraithRegistry.TryGetByNetworkId(definitionId, out WraithDefinition definition)) {
                    CompletePending(operation, false);
                    return;
                }
                key = definition.Key;
            }
            bool applied = ApplyAuthoritativeAttunement(Main.LocalPlayer, inventorySlot, instanceId,
                authoritativeRevision, key);
            CompletePending(operation, result == OnikiriNetResult.Success && applied);
        }

        private static OnikiriNetResult ValidateTarget(Player player, OnikiriData data,
            long instanceId, uint expectedRevision, int whoAmI) {
            if (player == null || data == null) {
                return OnikiriNetResult.InvalidItem;
            }
            if (data.InstanceId != instanceId) {
                return OnikiriNetResult.IdentityMismatch;
            }
            if (!AllowMutationRequest(whoAmI)) {
                return OnikiriNetResult.RateLimited;
            }
            return data.EditRevision == expectedRevision
                ? OnikiriNetResult.Success : OnikiriNetResult.StaleRevision;
        }

        private static void SendMeiResult(int toWho, ushort requestId, OnikiriNetResult result,
            byte inventorySlot, OnikiriData data, long requestedInstanceId, uint expectedRevision,
            byte rawSlot, string key) {
            ushort definitionId = NoDefinition;
            if (key != null) {
                OniMeiRegistry.TryGetNetworkId(key, out definitionId);
            }
            ModPacket packet = NewPacket(OnikiriNetOp.MeiResult);
            packet.Write(requestId);
            packet.Write((byte)result);
            packet.Write(inventorySlot);
            packet.Write(data?.InstanceId ?? requestedInstanceId);
            packet.Write(data?.EditRevision ?? expectedRevision);
            packet.Write(rawSlot);
            packet.Write(definitionId);
            packet.Send(toWho);
        }

        private static void SendAttuneResult(int toWho, ushort requestId, OnikiriNetResult result,
            byte inventorySlot, OnikiriData data, long requestedInstanceId, uint expectedRevision,
            string key) {
            ushort definitionId = NoDefinition;
            if (key != null) {
                WraithRegistry.TryGetNetworkId(key, out definitionId);
            }
            ModPacket packet = NewPacket(OnikiriNetOp.AttuneResult);
            packet.Write(requestId);
            packet.Write((byte)result);
            packet.Write(inventorySlot);
            packet.Write(data?.InstanceId ?? requestedInstanceId);
            packet.Write(data?.EditRevision ?? expectedRevision);
            packet.Write(definitionId);
            packet.Send(toWho);
        }

        private static ModPacket NewPacket(OnikiriNetOp op) {
            ModPacket packet = CWRMod.Instance.GetPacket();
            packet.Write((byte)CWRMessageType.OnikiriItemOperation);
            packet.Write((byte)op);
            return packet;
        }

        private static bool TryResolveLocalSlot(Player player, Item item, out byte slot) {
            slot = 0;
            if (ReferenceEquals(item, Main.mouseItem)) {
                slot = (byte)PlayerItemSlotID.InventoryMouseItem;
                return true;
            }
            int selected = player.selectedItem;
            if (selected >= 0 && selected < PlayerItemSlotID.InventoryMouseItem
                && selected < player.inventory.Length && ReferenceEquals(player.inventory[selected], item)) {
                slot = (byte)selected;
                return true;
            }
            return false;
        }

        internal static Item ResolveInventoryItem(Player player, int slot, bool localMouse) {
            if (player == null || slot < 0 || slot > PlayerItemSlotID.InventoryMouseItem
                || slot >= player.inventory.Length) {
                return null;
            }
            if (localMouse && player.whoAmI == Main.myPlayer
                && slot == PlayerItemSlotID.InventoryMouseItem) {
                return Main.mouseItem;
            }
            return player.inventory[slot];
        }

        private static Item ResolveServerTarget(Player player, byte slot) {
            if (player == null || slot > PlayerItemSlotID.InventoryMouseItem
                || slot != PlayerItemSlotID.InventoryMouseItem && slot != player.selectedItem) {
                return null;
            }
            return ResolveInventoryItem(player, slot, localMouse: false);
        }

        private static Item FindLocalIdentity(Player player, byte preferredSlot, long instanceId) {
            Item preferred = ResolveInventoryItem(player, preferredSlot, localMouse: true);
            if (HasInstance(preferred, instanceId)) {
                return preferred;
            }
            for (int i = 0; i <= PlayerItemSlotID.InventoryMouseItem && i < player.inventory.Length; i++) {
                if (HasInstance(player.inventory[i], instanceId)) {
                    return player.inventory[i];
                }
            }
            return HasInstance(Main.mouseItem, instanceId) ? Main.mouseItem : null;
        }

        private static bool HasInstance(Item item, long instanceId)
            => OnikiriData.TryGet(item)?.InstanceId == instanceId;

        private static bool ApplyAuthoritativeMei(Player player, byte preferredSlot, long instanceId,
            uint revision, OniMeiSlotKind slot, string key) {
            Item item = FindLocalIdentity(player, preferredSlot, instanceId);
            OnikiriData data = OnikiriData.TryGet(item);
            if (data == null || data.EditRevision > revision) {
                return false;
            }
            if (!ApplyMeiState(item, slot, key)) {
                return false;
            }
            data.ApplyEditRevision(revision);
            SyncMouseMirror(instanceId, target => ApplyMeiState(target, slot, key), revision);
            return true;
        }

        private static bool ApplyAuthoritativeAttunement(Player player, byte preferredSlot,
            long instanceId, uint revision, string key) {
            Item item = FindLocalIdentity(player, preferredSlot, instanceId);
            OnikiriData data = OnikiriData.TryGet(item);
            if (data == null || data.EditRevision > revision) {
                return false;
            }
            data.Wraiths.ApplyAttunedKey(key);
            data.ApplyEditRevision(revision);
            SyncMouseMirror(instanceId, target => ApplyAttunedState(target, key), revision);
            return true;
        }

        private static void SyncMouseMirror(long instanceId, System.Action<Item> apply, uint revision) {
            Item mouse = Main.mouseItem;
            Item mirror = Main.LocalPlayer?.inventory.Length > PlayerItemSlotID.InventoryMouseItem
                ? Main.LocalPlayer.inventory[PlayerItemSlotID.InventoryMouseItem] : null;
            if (HasInstance(mouse, instanceId)) {
                apply(mouse);
                OnikiriData.TryGet(mouse)?.ApplyEditRevision(revision);
            }
            if (!ReferenceEquals(mirror, mouse) && HasInstance(mirror, instanceId)) {
                apply(mirror);
                OnikiriData.TryGet(mirror)?.ApplyEditRevision(revision);
            }
        }

        private static bool ApplyMeiState(Item item, OniMeiSlotKind slot, string key) {
            OnikiriData data = OnikiriData.TryGet(item);
            if (data == null) {
                return false;
            }
            string current = data.Mei.Get(slot);
            if (current == key) {
                return true;
            }
            return key == null ? data.Mei.Erase(slot) : data.Mei.Engrave(slot, key);
        }

        private static void ApplyAttunedState(Item item, string key) {
            OnikiriData.TryGet(item)?.Wraiths.ApplyAttunedKey(key);
        }

        private static bool CanAttune(WraithProgressStore store, WraithDefinition definition) {
            return store != null && definition?.CanAttune == true
                && store.TryGet(definition.Key, out WraithProgressRecord record)
                && record.State == WraithBindState.Bound;
        }

        internal static void RecordAuthoritativeState(Player player, OnikiriData data) {
            if (!VaultUtils.isServer || player == null || data == null || data.InstanceId == 0) {
                return;
            }
            var key = (player.whoAmI, data.InstanceId);
            if (!authoritativeEdits.TryGetValue(key, out AuthoritativeEditState state)) {
                state = new AuthoritativeEditState();
                authoritativeEdits.Add(key, state);
            }
            state.Capture(data);
        }

        internal static void ReconcileAuthoritativeState(Player player) {
            if (!VaultUtils.isServer || player?.active != true) {
                return;
            }
            ulong now = Main.GameUpdateCount;
            foreach (var key in new List<(int Player, long InstanceId)>(authoritativeEdits.Keys)) {
                if (key.Player != player.whoAmI) {
                    continue;
                }
                AuthoritativeEditState state = authoritativeEdits[key];
                if (now - state.CapturedAt > AuthorityLifetimeTicks) {
                    authoritativeEdits.Remove(key);
                    continue;
                }
                for (int slot = 0; slot <= PlayerItemSlotID.InventoryMouseItem
                    && slot < player.inventory.Length; slot++) {
                    OnikiriData data = OnikiriData.TryGet(player.inventory[slot]);
                    if (data?.InstanceId == key.InstanceId && data.EditRevision < state.Revision) {
                        data.ApplyEditedState(state.Wraiths, state.Mei, state.Revision);
                    }
                }
            }
        }

        internal static void ResetPlayerSession(Player player) {
            if (player == null) {
                return;
            }
            if (player.whoAmI == Main.myPlayer) {
                List<PendingOperation> abandoned = [.. pending.Values];
                pending.Clear();
                nextRequestId = 0;
                foreach (PendingOperation operation in abandoned) {
                    CompletePending(operation, false);
                }
            }
            requestWindows.Remove(player.whoAmI);
            foreach (var key in new List<(int Player, long InstanceId)>(authoritativeEdits.Keys)) {
                if (key.Player == player.whoAmI) {
                    authoritativeEdits.Remove(key);
                }
            }
        }

        internal static void UpdatePending(Player player) {
            if (VaultUtils.isClient && player?.whoAmI == Main.myPlayer) {
                SweepPending();
            }
        }

        private static bool TryTrackPending(OnikiriNetOp operation, byte inventorySlot, byte field,
            long instanceId, uint expectedRevision, Action<bool> completed, out ushort requestId) {
            SweepPending();
            requestId = 0;
            if (pending.Count >= MaxPendingOperations) {
                return false;
            }
            foreach (PendingOperation active in pending.Values) {
                if (active.InstanceId == instanceId) {
                    return false;
                }
            }
            do {
                requestId = ++nextRequestId;
            }
            while (requestId == 0 || pending.ContainsKey(requestId));
            pending[requestId] = new PendingOperation {
                Operation = operation,
                InventorySlot = inventorySlot,
                Field = field,
                InstanceId = instanceId,
                ExpectedRevision = expectedRevision,
                CreatedAt = Main.GameUpdateCount,
                Completion = completed,
            };
            return true;
        }

        private static PendingOperation TakePending(ushort requestId) {
            SweepPending();
            return pending.Remove(requestId, out PendingOperation operation) ? operation : null;
        }

        private static void SweepPending() {
            if (pending.Count == 0) {
                return;
            }
            ulong now = Main.GameUpdateCount;
            List<PendingOperation> expired = [];
            foreach (ushort id in new List<ushort>(pending.Keys)) {
                if (now - pending[id].CreatedAt > PendingLifetimeTicks) {
                    expired.Add(pending[id]);
                    pending.Remove(id);
                }
            }
            foreach (PendingOperation operation in expired) {
                CompletePending(operation, false);
            }
        }

        private static void CompletePending(PendingOperation operation, bool success) {
            Action<bool> completion = operation?.Completion;
            if (operation != null) {
                operation.Completion = null;
            }
            completion?.Invoke(success);
        }

        private static bool AllowMutationRequest(int whoAmI) {
            ulong now = Main.GameUpdateCount;
            if (!requestWindows.TryGetValue(whoAmI, out RequestWindow window)
                || now - window.StartedAt >= RequestWindowTicks) {
                requestWindows[whoAmI] = new RequestWindow { StartedAt = now, Count = 1 };
                return true;
            }
            if (window.Count >= MaxRequestsPerWindow) {
                return false;
            }
            window.Count++;
            requestWindows[whoAmI] = window;
            return true;
        }

        private static Player ResolveSender(int whoAmI) {
            if (whoAmI < 0 || whoAmI >= Main.maxPlayers) {
                return null;
            }
            Player player = Main.player[whoAmI];
            return player?.active == true && !player.dead ? player : null;
        }
    }
}

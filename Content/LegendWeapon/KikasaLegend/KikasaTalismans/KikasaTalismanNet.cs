using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaTalismans
{
    internal enum KikasaTalismanNetOp : byte
    {
        OwnedSnapshot = 0,
        TalismanRequest = 1,
        TalismanResult = 2,
    }

    internal enum KikasaTalismanNetResult : byte
    {
        Success = 0,
        InvalidItem = 1,
        IdentityMismatch = 2,
        StaleRevision = 3,
        InvalidDefinition = 4,
        NotOwned = 5,
        DuplicateKey = 6,
        RateLimited = 7,
        DuplicateIdentity = 8,
    }

    /// <summary>鬼伞挂符字段的物品级权威同步（协议形状镜像 OnikiriNet）</summary>
    internal static class KikasaTalismanNet
    {
        private const ushort NoDefinition = ushort.MaxValue;
        private const int MaxPendingOperations = 64;
        private const ulong PendingLifetimeTicks = 600;
        private const ulong RequestWindowTicks = 60;
        private const int MaxRequestsPerWindow = 12;
        private const ulong AuthorityLifetimeTicks = 600;

        private sealed class PendingOperation
        {
            internal byte InventorySlot;
            internal byte TalismanSlot;
            internal long InstanceId;
            internal uint ExpectedRevision;
            internal ulong CreatedAt;
            internal Action<bool> Completion;
        }

        private struct RequestWindow
        {
            internal ulong StartedAt;
            internal int Count;
        }

        private sealed class AuthoritativeEditState
        {
            internal readonly KikasaTalismanStore Talismans = new();
            internal uint Revision;
            internal ulong CapturedAt;

            internal void Capture(KikasaData data) {
                Talismans.CopyFrom(data.Talismans);
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
            if (type != CWRMessageType.KikasaTalisman) {
                return;
            }

            KikasaTalismanNetOp op = (KikasaTalismanNetOp)reader.ReadByte();
            if (Main.netMode == NetmodeID.Server) {
                switch (op) {
                    case KikasaTalismanNetOp.OwnedSnapshot:
                        ReceiveOwnedSnapshot(reader, whoAmI);
                        break;
                    case KikasaTalismanNetOp.TalismanRequest:
                        ReceiveTalismanRequest(reader, whoAmI);
                        break;
                }
                return;
            }
            if (Main.netMode == NetmodeID.MultiplayerClient
                && op == KikasaTalismanNetOp.TalismanResult) {
                ReceiveTalismanResult(reader);
            }
        }

        /// <summary>
        /// 挂/摘手中伞符位（key=null 摘符）。本机预检与服务端复检共用
        /// <see cref="KikasaTalismanStore.Hang"/> 的注册+去重谓词
        /// </summary>
        public static bool TryChangeTalisman(Player player, Item item, int slot, string key,
            Action<bool> completed = null) {
            if (player == null || player.whoAmI != Main.myPlayer
                || slot < 0 || slot >= KikasaTalismanStore.SlotCount) {
                return false;
            }
            RepairDuplicateIdentities(player);
            KikasaData data = KikasaData.TryGet(item);
            if (data == null || HasDuplicateInstanceId(player, data.InstanceId)
                || !TryResolveLocalSlot(player, item, out byte inventorySlot)) {
                return false;
            }

            ushort definitionId = NoDefinition;
            if (key != null) {
                if (!KikasaTalismanOwned.Owns(player, key)
                    || !KikasaTalismanRegistry.TryGet(key, out _)
                    || !KikasaTalismanRegistry.TryGetNetworkId(key, out definitionId)) {
                    return false;
                }
                //同符已挂他位：换位走"先摘后挂"两步，这里直接拒绝
                if (data.Talismans.Contains(key) && data.Talismans.Get(slot) != key) {
                    return false;
                }
            }
            if (data.Talismans.Get(slot) == key) {
                return false;
            }

            if (Main.netMode != NetmodeID.MultiplayerClient) {
                if (!ApplyTalismanState(item, slot, key)) {
                    return false;
                }
                data.AdvanceEditRevision();
                completed?.Invoke(true);
                return true;
            }

            if (!TryTrackPending(inventorySlot, (byte)slot, data.InstanceId,
                data.EditRevision, completed, out ushort requestId)) {
                return false;
            }
            ModPacket packet = NewPacket(KikasaTalismanNetOp.TalismanRequest);
            packet.Write(requestId);
            packet.Write(inventorySlot);
            packet.Write(data.InstanceId);
            packet.Write(data.EditRevision);
            packet.Write((byte)slot);
            packet.Write(definitionId);
            packet.Send();
            return true;
        }

        /// <summary>符箧快照推给服务器，让服务端那份 ModPlayer 与本机读数一致</summary>
        public static void SendOwnedSnapshot(Player player) {
            if (Main.netMode != NetmodeID.MultiplayerClient || player == null
                || player.whoAmI != Main.myPlayer
                || !player.TryGetModPlayer(out KikasaTalismanPlayer ktp)) {
                return;
            }
            KikasaTalismanOwned.EnsureInit(ktp);
            HashSet<ushort> uniqueIds = [];
            foreach (string key in ktp.OwnedTalismanKeys) {
                if (KikasaTalismanRegistry.TryGetNetworkId(key, out ushort id)) {
                    uniqueIds.Add(id);
                }
            }
            List<ushort> ids = [.. uniqueIds];
            ids.Sort();

            ModPacket packet = NewPacket(KikasaTalismanNetOp.OwnedSnapshot);
            packet.Write((ushort)ids.Count);
            foreach (ushort id in ids) {
                packet.Write(id);
            }
            packet.Send();
        }

        private static void ReceiveOwnedSnapshot(BinaryReader reader, int whoAmI) {
            //先把负载读干净再做守卫：提前 return 会在 HandlePacket 留下未读字节，
            //既刷 Read underflow 又把这次符箧同步整份丢掉
            int count = reader.ReadUInt16();
            List<string> keys = new(Math.Min(count, KikasaTalismanRegistry.All.Count));
            for (int i = 0; i < count; i++) {
                if (KikasaTalismanRegistry.TryGetByNetworkId(reader.ReadUInt16(),
                    out KikasaTalismanDefinition definition)) {
                    keys.Add(definition.Key);
                }
            }
            //符箧是纯存档状态，进世界那帧玩家还没落地，不能按存活筛
            Player player = ResolveSender(whoAmI, requireAlive: false);
            if (player == null || count > KikasaTalismanRegistry.All.Count) {
                return;
            }
            KikasaTalismanOwned.ApplyNetworkSnapshot(player, keys);
        }

        private static void ReceiveTalismanRequest(BinaryReader reader, int whoAmI) {
            ushort requestId = reader.ReadUInt16();
            byte inventorySlot = reader.ReadByte();
            long requestedInstanceId = reader.ReadInt64();
            uint expectedRevision = reader.ReadUInt32();
            byte rawSlot = reader.ReadByte();
            ushort definitionId = reader.ReadUInt16();

            Player player = ResolveSender(whoAmI);
            Item item = ResolveServerTarget(player, inventorySlot);
            KikasaData data = KikasaData.TryGet(item);
            KikasaTalismanNetResult result = ValidateTarget(player, data, requestedInstanceId,
                expectedRevision, whoAmI);

            if (result == KikasaTalismanNetResult.Success) {
                if (rawSlot >= KikasaTalismanStore.SlotCount) {
                    result = KikasaTalismanNetResult.InvalidDefinition;
                }
                else {
                    string key = null;
                    if (definitionId != NoDefinition) {
                        if (!KikasaTalismanRegistry.TryGetByNetworkId(definitionId,
                            out KikasaTalismanDefinition definition)) {
                            result = KikasaTalismanNetResult.InvalidDefinition;
                        }
                        else if (!KikasaTalismanOwned.Owns(player, definition.Key)) {
                            result = KikasaTalismanNetResult.NotOwned;
                        }
                        else if (data.Talismans.Contains(definition.Key)
                            && data.Talismans.Get(rawSlot) != definition.Key) {
                            result = KikasaTalismanNetResult.DuplicateKey;
                        }
                        else {
                            key = definition.Key;
                        }
                    }
                    if (result == KikasaTalismanNetResult.Success && data.Talismans.Get(rawSlot) != key) {
                        if (!ApplyTalismanState(item, rawSlot, key)) {
                            result = KikasaTalismanNetResult.InvalidDefinition;
                        }
                        else {
                            data.AdvanceEditRevision();
                        }
                    }
                }
            }

            if (result != KikasaTalismanNetResult.Success) {
                //拒绝必须可诊断：静默 false 会让下一个排障者读一整晚代码
                CWRMod.Instance.Logger.Info(
                    $"[KikasaTalismanNet] reject {result} from player {whoAmI}, slot {rawSlot}, def {definitionId}");
            }

            string currentKey = data != null && data.InstanceId == requestedInstanceId
                && rawSlot < KikasaTalismanStore.SlotCount
                ? data.Talismans.Get(rawSlot) : null;
            if (result == KikasaTalismanNetResult.Success) {
                RecordAuthoritativeState(player, data);
            }
            SendTalismanResult(whoAmI, requestId, result, inventorySlot, data,
                requestedInstanceId, expectedRevision, rawSlot, currentKey);
        }

        private static void ReceiveTalismanResult(BinaryReader reader) {
            ushort requestId = reader.ReadUInt16();
            KikasaTalismanNetResult result = (KikasaTalismanNetResult)reader.ReadByte();
            byte inventorySlot = reader.ReadByte();
            long instanceId = reader.ReadInt64();
            uint authoritativeRevision = reader.ReadUInt32();
            byte rawSlot = reader.ReadByte();
            ushort definitionId = reader.ReadUInt16();
            PendingOperation operation = TakePending(requestId);
            if (operation == null) {
                return;
            }
            if (operation.InventorySlot != inventorySlot || operation.TalismanSlot != rawSlot
                || rawSlot >= KikasaTalismanStore.SlotCount
                || result > KikasaTalismanNetResult.DuplicateIdentity
                || authoritativeRevision < operation.ExpectedRevision
                || result is KikasaTalismanNetResult.InvalidItem
                    or KikasaTalismanNetResult.IdentityMismatch
                    or KikasaTalismanNetResult.DuplicateIdentity
                || operation.InstanceId != instanceId) {
                CompletePending(operation, false);
                return;
            }

            string key = null;
            if (definitionId != NoDefinition) {
                if (!KikasaTalismanRegistry.TryGetByNetworkId(definitionId,
                    out KikasaTalismanDefinition definition)) {
                    CompletePending(operation, false);
                    return;
                }
                key = definition.Key;
            }
            bool applied = ApplyAuthoritativeTalisman(Main.LocalPlayer, inventorySlot, instanceId,
                authoritativeRevision, rawSlot, key);
            CompletePending(operation, result == KikasaTalismanNetResult.Success && applied);
        }

        private static KikasaTalismanNetResult ValidateTarget(Player player, KikasaData data,
            long instanceId, uint expectedRevision, int whoAmI) {
            if (player == null || data == null) {
                return KikasaTalismanNetResult.InvalidItem;
            }
            if (data.InstanceId != instanceId) {
                return KikasaTalismanNetResult.IdentityMismatch;
            }
            if (HasDuplicateInstanceId(player, instanceId)) {
                return KikasaTalismanNetResult.DuplicateIdentity;
            }
            if (!AllowMutationRequest(whoAmI)) {
                return KikasaTalismanNetResult.RateLimited;
            }
            return data.EditRevision == expectedRevision
                ? KikasaTalismanNetResult.Success : KikasaTalismanNetResult.StaleRevision;
        }

        private static void SendTalismanResult(int toWho, ushort requestId, KikasaTalismanNetResult result,
            byte inventorySlot, KikasaData data, long requestedInstanceId,
            uint expectedRevision, byte rawSlot, string key) {
            ushort definitionId = NoDefinition;
            if (key != null) {
                KikasaTalismanRegistry.TryGetNetworkId(key, out definitionId);
            }
            ModPacket packet = NewPacket(KikasaTalismanNetOp.TalismanResult);
            packet.Write(requestId);
            packet.Write((byte)result);
            packet.Write(inventorySlot);
            packet.Write(data?.InstanceId ?? requestedInstanceId);
            packet.Write(data?.EditRevision ?? expectedRevision);
            packet.Write(rawSlot);
            packet.Write(definitionId);
            packet.Send(toWho);
        }

        private static ModPacket NewPacket(KikasaTalismanNetOp op) {
            ModPacket packet = CWRMod.Instance.GetPacket();
            packet.Write((byte)CWRMessageType.KikasaTalisman);
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
                && selected < player.inventory.Length
                && ReferenceEquals(player.inventory[selected], item)) {
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
            for (int i = 0; i <= PlayerItemSlotID.InventoryMouseItem
                && i < player.inventory.Length; i++) {
                if (HasInstance(player.inventory[i], instanceId)) {
                    return player.inventory[i];
                }
            }
            return HasInstance(Main.mouseItem, instanceId) ? Main.mouseItem : null;
        }

        private static bool HasInstance(Item item, long instanceId)
            => instanceId != 0 && KikasaData.TryGet(item)?.InstanceId == instanceId;

        private static bool ApplyAuthoritativeTalisman(Player player, byte preferredSlot,
            long instanceId, uint revision, int slot, string key) {
            Item item = FindLocalIdentity(player, preferredSlot, instanceId);
            KikasaData data = KikasaData.TryGet(item);
            if (data == null || data.EditRevision > revision || !ApplyTalismanState(item, slot, key)) {
                return false;
            }
            data.ApplyEditRevision(revision);
            SyncMouseMirror(instanceId, target => ApplyTalismanState(target, slot, key), revision);
            return true;
        }

        private static void SyncMouseMirror(long instanceId, Action<Item> apply, uint revision) {
            Item mouse = Main.mouseItem;
            Item mirror = Main.LocalPlayer?.inventory.Length > PlayerItemSlotID.InventoryMouseItem
                ? Main.LocalPlayer.inventory[PlayerItemSlotID.InventoryMouseItem] : null;
            if (HasInstance(mouse, instanceId)) {
                apply(mouse);
                KikasaData.TryGet(mouse)?.ApplyEditRevision(revision);
            }
            if (!ReferenceEquals(mirror, mouse) && HasInstance(mirror, instanceId)) {
                apply(mirror);
                KikasaData.TryGet(mirror)?.ApplyEditRevision(revision);
            }
        }

        private static bool ApplyTalismanState(Item item, int slot, string key) {
            KikasaData data = KikasaData.TryGet(item);
            if (data == null) {
                return false;
            }
            string current = data.Talismans.Get(slot);
            if (current == key) {
                return true;
            }
            return key == null ? data.Talismans.TakeDown(slot) : data.Talismans.Hang(slot, key);
        }

        internal static void RecordAuthoritativeState(Player player, KikasaData data) {
            if (Main.netMode != NetmodeID.Server || player == null || data == null
                || data.InstanceId == 0) {
                return;
            }
            var key = (player.whoAmI, data.InstanceId);
            if (!authoritativeEdits.TryGetValue(key, out AuthoritativeEditState state)) {
                state = new AuthoritativeEditState();
                authoritativeEdits.Add(key, state);
            }
            state.Capture(data);
        }

        /// <summary>服务端逐帧回填：客户端迟到的旧物品同步不许吃掉已批准的挂符</summary>
        internal static void ReconcileAuthoritativeState(Player player) {
            if (Main.netMode != NetmodeID.Server || player?.active != true) {
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
                if (HasDuplicateInstanceId(player, key.InstanceId)) {
                    continue;
                }
                for (int slot = 0; slot <= PlayerItemSlotID.InventoryMouseItem
                    && slot < player.inventory.Length; slot++) {
                    KikasaData data = KikasaData.TryGet(player.inventory[slot]);
                    if (data?.InstanceId == key.InstanceId && data.EditRevision < state.Revision) {
                        data.ApplyEditedState(state.Talismans, state.Revision);
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
            if (Main.netMode == NetmodeID.MultiplayerClient
                && player?.whoAmI == Main.myPlayer) {
                SweepPending();
            }
        }

        /// <summary>克隆/复制造成的同 InstanceId 伞在本机就地换新身份，并推物品同步</summary>
        internal static bool RepairDuplicateIdentities(Player player) {
            if (player == null || player.whoAmI != Main.myPlayer
                || Main.netMode == NetmodeID.Server) {
                return false;
            }

            bool repaired = false;
            HashSet<long> identities = [];
            HashSet<Item> entities = [];
            int lastSlot = Math.Min(PlayerItemSlotID.InventoryMouseItem,
                player.inventory.Length - 1);
            for (int slot = 0; slot <= lastSlot; slot++) {
                Item item = player.inventory[slot];
                KikasaData data = KikasaData.TryGet(item);
                if (data == null || !entities.Add(item)) {
                    continue;
                }
                if (data.InstanceId != 0 && identities.Add(data.InstanceId)) {
                    continue;
                }
                do {
                    data.RenewIdentity();
                }
                while (!identities.Add(data.InstanceId));
                repaired = true;
                if (Main.netMode == NetmodeID.MultiplayerClient) {
                    int slotId = slot == PlayerItemSlotID.InventoryMouseItem
                        ? PlayerItemSlotID.InventoryMouseItem
                        : PlayerItemSlotID.Inventory0 + slot;
                    NetMessage.SendData(MessageID.SyncEquipment, -1, -1, null,
                        player.whoAmI, slotId, item.prefix);
                }
            }
            return repaired;
        }

        internal static bool HasDuplicateInstanceId(Player player, long instanceId) {
            if (player == null || instanceId == 0) {
                return true;
            }
            HashSet<Item> entities = [];
            int matches = 0;
            int lastSlot = Math.Min(PlayerItemSlotID.InventoryMouseItem,
                player.inventory.Length - 1);
            for (int slot = 0; slot <= lastSlot; slot++) {
                Item item = player.inventory[slot];
                if (!entities.Add(item)) {
                    continue;
                }
                if (KikasaData.TryGet(item)?.InstanceId == instanceId && ++matches > 1) {
                    return true;
                }
            }
            return false;
        }

        private static bool TryTrackPending(byte inventorySlot, byte talismanSlot,
            long instanceId, uint expectedRevision, Action<bool> completed,
            out ushort requestId) {
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
                InventorySlot = inventorySlot,
                TalismanSlot = talismanSlot,
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
                requestWindows[whoAmI] = new RequestWindow {
                    StartedAt = now,
                    Count = 1,
                };
                return true;
            }
            if (window.Count >= MaxRequestsPerWindow) {
                return false;
            }
            window.Count++;
            requestWindows[whoAmI] = window;
            return true;
        }

        private static Player ResolveSender(int whoAmI, bool requireAlive = true) {
            if (whoAmI < 0 || whoAmI >= Main.maxPlayers) {
                return null;
            }
            Player player = Main.player[whoAmI];
            return player?.active == true && (!requireAlive || !player.dead)
                ? player
                : null;
        }
    }
}

using CalamityOverhaul.Content.Cyberwares.Victors;
using CalamityOverhaul.Content.TimeFreezes;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Cyberwares
{
    internal readonly record struct CyberwareLoadoutSnapshot(
        int PlayerIndex,
        uint SessionGeneration,
        uint LoadoutRevision,
        int[] ItemTypes)
    {
        internal bool IsStructurallyValid {
            get {
                if (PlayerIndex < 0 || PlayerIndex >= Main.maxPlayers
                    || SessionGeneration == 0 || LoadoutRevision == 0
                    || ItemTypes == null
                    || ItemTypes.Length != CyberwarePlayer.SlotCount) {
                    return false;
                }
                for (int i = 0; i < ItemTypes.Length; i++) {
                    if (ItemTypes[i] < ItemID.None
                        || ItemTypes[i] >= ItemLoader.ItemCount) {
                        return false;
                    }
                }
                return true;
            }
        }
    }

    internal static class CyberwareNet
    {
        private enum CyberwareNetOp : byte
        {
            InitialProfile = 1,
            LoadoutSnapshot = 2,
            SurgeryRequest = 3,
            PurchaseRequest = 4,
            RequestResult = 5,
        }

        private sealed class PendingVictorRequest
        {
            internal uint SessionGeneration;
            internal VictorRequestKind Kind;
            internal ulong CreatedAt;
            internal Action<VictorRequestResult> Completion;
        }

        private readonly record struct WalletSlotSnapshot(
            Item[] Container,
            int Index,
            int NetworkSlot,
            Item Item);

        private const int MainInventorySlotCount = 50;
        private const int MaxPendingRequests = 16;
        private const ulong PendingLifetimeFrames = 600;
        private const float MaxVictorDistance = 320f;

        private static readonly Dictionary<uint, PendingVictorRequest> pendingRequests = [];
        private static uint nextSessionGeneration;

        internal static uint AllocateSessionGeneration() {
            nextSessionGeneration++;
            if (nextSessionGeneration == 0) {
                nextSessionGeneration = 1;
            }
            return nextSessionGeneration;
        }

        internal static void SendInitialProfile(CyberwarePlayer state) {
            if (Main.netMode != NetmodeID.MultiplayerClient || state == null
                || state.Player.whoAmI != Main.myPlayer) {
                return;
            }

            ModPacket packet = NewPacket(CyberwareNetOp.InitialProfile);
            WriteTypes(packet, state.CaptureLoadoutTypes());
            packet.Send();
        }

        internal static void SendLoadoutSnapshot(Player player, int toWho = -1) {
            if (Main.netMode != NetmodeID.Server || !TryResolvePlayer(player?.whoAmI ?? -1,
                requireAlive: false, out Player resolved)
                || toWho < -1 || toWho >= Main.maxPlayers) {
                return;
            }
            CyberwarePlayer state = resolved.GetModPlayer<CyberwarePlayer>();
            if (!state.ProfileInitialized) {
                return;
            }

            ModPacket packet = NewPacket(CyberwareNetOp.LoadoutSnapshot);
            WriteSnapshot(packet, CaptureSnapshot(resolved, state));
            packet.Send(toWho);
        }

        internal static bool SendSurgeryRequest(Player player, int victorWhoAmI,
            VictorRequestKind kind, int inventorySlot, int loadoutSlot,
            Action<VictorRequestResult> completion) {
            if (Main.netMode == NetmodeID.Server || player?.active != true
                || player.dead || player.whoAmI != Main.myPlayer
                || kind is not (VictorRequestKind.Install
                    or VictorRequestKind.Uninstall)
                || loadoutSlot < 0 || loadoutSlot >= CyberwarePlayer.SlotCount
                || inventorySlot < -1 || inventorySlot > byte.MaxValue - 1
                || !TryCaptureVictor(victorWhoAmI,
                    out NetworkNPCIdentity victorIdentity)) {
                return false;
            }

            CyberwarePlayer state = player.GetModPlayer<CyberwarePlayer>();
            if (!state.TryAllocateVictorRequest(kind, out VictorRequestToken token)
                || !TryRegisterPending(token, kind, completion)) {
                return false;
            }

            int wireInventorySlot = kind == VictorRequestKind.Install
                ? inventorySlot
                : byte.MaxValue;
            if (kind == VictorRequestKind.Install
                && (wireInventorySlot < 0 || wireInventorySlot >= player.inventory.Length)) {
                CancelPending(token.RequestId);
                return false;
            }

            if (Main.netMode == NetmodeID.SinglePlayer) {
                ProcessSurgeryRequest(player, token.SessionGeneration,
                    token.RequestId, token.LoadoutRevision, victorIdentity, kind,
                    loadoutSlot, wireInventorySlot, player.whoAmI);
                return true;
            }

            ModPacket packet = NewPacket(CyberwareNetOp.SurgeryRequest);
            packet.Write(token.SessionGeneration);
            packet.Write(token.RequestId);
            packet.Write(token.LoadoutRevision);
            victorIdentity.Write(packet);
            packet.Write((byte)kind);
            packet.Write((byte)loadoutSlot);
            packet.Write((byte)wireInventorySlot);
            packet.Send();
            return true;
        }

        internal static bool SendPurchaseRequest(Player player, int victorWhoAmI,
            int loadoutSlot, int itemType,
            Action<VictorRequestResult> completion) {
            if (Main.netMode == NetmodeID.Server || player?.active != true
                || player.dead || player.whoAmI != Main.myPlayer
                || loadoutSlot < 0 || loadoutSlot >= CyberwarePlayer.SlotCount
                || itemType <= ItemID.None || itemType >= ItemLoader.ItemCount
                || !TryCaptureVictor(victorWhoAmI,
                    out NetworkNPCIdentity victorIdentity)) {
                return false;
            }

            CyberwarePlayer state = player.GetModPlayer<CyberwarePlayer>();
            const VictorRequestKind kind = VictorRequestKind.Purchase;
            if (!state.TryAllocateVictorRequest(kind, out VictorRequestToken token)
                || !TryRegisterPending(token, kind, completion)) {
                return false;
            }

            if (Main.netMode == NetmodeID.SinglePlayer) {
                ProcessPurchaseRequest(player, token.SessionGeneration,
                    token.RequestId, token.LoadoutRevision, victorIdentity,
                    loadoutSlot, itemType, player.whoAmI);
                return true;
            }

            ModPacket packet = NewPacket(CyberwareNetOp.PurchaseRequest);
            packet.Write(token.SessionGeneration);
            packet.Write(token.RequestId);
            packet.Write(token.LoadoutRevision);
            victorIdentity.Write(packet);
            packet.Write((byte)loadoutSlot);
            packet.Write(itemType);
            packet.Send();
            return true;
        }

        internal static void NetHandle(CWRMessageType type, BinaryReader reader,
            int whoAmI) {
            if (type != CWRMessageType.Cyberware) {
                return;
            }

            try {
                CyberwareNetOp operation = (CyberwareNetOp)reader.ReadByte();
                switch (operation) {
                    case CyberwareNetOp.InitialProfile:
                        HandleInitialProfile(reader, whoAmI);
                        break;
                    case CyberwareNetOp.LoadoutSnapshot:
                        HandleLoadoutSnapshot(reader);
                        break;
                    case CyberwareNetOp.SurgeryRequest:
                        HandleSurgeryRequest(reader, whoAmI);
                        break;
                    case CyberwareNetOp.PurchaseRequest:
                        HandlePurchaseRequest(reader, whoAmI);
                        break;
                    case CyberwareNetOp.RequestResult:
                        HandleRequestResult(reader);
                        break;
                }
            } catch (EndOfStreamException) {
            } catch (IOException) {
            }
        }

        internal static void UpdatePendingRequests() {
            if (pendingRequests.Count == 0) {
                return;
            }

            ulong now = Main.GameUpdateCount;
            List<uint> expired = [];
            foreach ((uint requestId, PendingVictorRequest pending) in pendingRequests) {
                if (now - pending.CreatedAt > PendingLifetimeFrames) {
                    expired.Add(requestId);
                }
            }
            foreach (uint requestId in expired) {
                if (pendingRequests.Remove(requestId,
                    out PendingVictorRequest pending)) {
                    pending.Completion?.Invoke(default);
                }
            }
        }

        internal static void Reset() {
            pendingRequests.Clear();
            nextSessionGeneration = 0;
        }

        internal static void DropPendingExcept(uint sessionGeneration) {
            if (pendingRequests.Count == 0) {
                return;
            }
            List<uint> dropped = [];
            foreach ((uint requestId, PendingVictorRequest pending) in pendingRequests) {
                if (pending.SessionGeneration != sessionGeneration) {
                    dropped.Add(requestId);
                }
            }
            foreach (uint requestId in dropped) {
                if (pendingRequests.Remove(requestId,
                    out PendingVictorRequest pending)) {
                    pending.Completion?.Invoke(default);
                }
            }
        }

        private static void HandleInitialProfile(BinaryReader reader, int whoAmI) {
            int[] submittedTypes = ReadTypes(reader);
            if (Main.netMode != NetmodeID.Server
                || !TryResolvePlayer(whoAmI, requireAlive: false,
                    out Player player)) {
                return;
            }

            CyberwarePlayer state = player.GetModPlayer<CyberwarePlayer>();
            if (state.ProfileInitialized) {
                SendLoadoutSnapshot(player, whoAmI);
                return;
            }
            state.InitializeAuthorityProfile(submittedTypes,
                AllocateSessionGeneration());

            SendLoadoutSnapshot(player);
            for (int i = 0; i < Main.maxPlayers; i++) {
                if (i == whoAmI || Main.player[i]?.active != true) {
                    continue;
                }
                CyberwarePlayer otherState = Main.player[i]
                    .GetModPlayer<CyberwarePlayer>();
                if (otherState.ProfileInitialized) {
                    SendLoadoutSnapshot(Main.player[i], whoAmI);
                }
            }
        }

        private static void HandleLoadoutSnapshot(BinaryReader reader) {
            CyberwareLoadoutSnapshot snapshot = ReadSnapshot(reader);
            if (Main.netMode != NetmodeID.MultiplayerClient
                || !snapshot.IsStructurallyValid) {
                return;
            }

            Player player = Main.player[snapshot.PlayerIndex];
            if (player?.active != true) {
                return;
            }
            player.GetModPlayer<CyberwarePlayer>().ApplyAuthoritySnapshot(
                snapshot.SessionGeneration, snapshot.LoadoutRevision,
                snapshot.ItemTypes);
        }

        private static void HandleSurgeryRequest(BinaryReader reader, int whoAmI) {
            uint sessionGeneration = reader.ReadUInt32();
            uint requestId = reader.ReadUInt32();
            uint expectedRevision = reader.ReadUInt32();
            NetworkNPCIdentity.TryRead(reader,
                out NetworkNPCIdentity victorIdentity);
            VictorRequestKind kind = (VictorRequestKind)reader.ReadByte();
            int loadoutSlot = reader.ReadByte();
            int inventorySlot = reader.ReadByte();

            if (Main.netMode != NetmodeID.Server
                || !TryResolvePlayer(whoAmI, requireAlive: false,
                    out Player player)) {
                return;
            }
            ProcessSurgeryRequest(player, sessionGeneration, requestId,
                expectedRevision, victorIdentity, kind, loadoutSlot,
                inventorySlot, whoAmI);
        }

        private static void HandlePurchaseRequest(BinaryReader reader, int whoAmI) {
            uint sessionGeneration = reader.ReadUInt32();
            uint requestId = reader.ReadUInt32();
            uint expectedRevision = reader.ReadUInt32();
            NetworkNPCIdentity.TryRead(reader,
                out NetworkNPCIdentity victorIdentity);
            int loadoutSlot = reader.ReadByte();
            int itemType = reader.ReadInt32();

            if (Main.netMode != NetmodeID.Server
                || !TryResolvePlayer(whoAmI, requireAlive: false,
                    out Player player)) {
                return;
            }
            ProcessPurchaseRequest(player, sessionGeneration, requestId,
                expectedRevision, victorIdentity, loadoutSlot, itemType,
                whoAmI);
        }

        private static void HandleRequestResult(BinaryReader reader) {
            VictorRequestResult result = ReadResult(reader);
            CyberwareLoadoutSnapshot snapshot = ReadSnapshot(reader);
            if (Main.netMode != NetmodeID.MultiplayerClient
                || !snapshot.IsStructurallyValid
                || snapshot.PlayerIndex != Main.myPlayer) {
                return;
            }

            Player player = Main.player[snapshot.PlayerIndex];
            CyberwarePlayer state = player?.active == true
                ? player.GetModPlayer<CyberwarePlayer>()
                : null;
            if (state == null || !state.ApplyAuthoritySnapshot(
                snapshot.SessionGeneration, snapshot.LoadoutRevision,
                snapshot.ItemTypes) || !result.IsValid
                || !IsRevisionAtLeast(snapshot.LoadoutRevision,
                    result.AuthorityLoadoutRevision)) {
                return;
            }

            state.StoreVictorRequestResult(result);
            CompletePending(result);
        }

        private static void ProcessSurgeryRequest(Player player,
            uint sessionGeneration, uint requestId, uint expectedRevision,
            in NetworkNPCIdentity victorIdentity, VictorRequestKind kind,
            int loadoutSlot, int inventorySlot, int replyTo) {
            CyberwarePlayer state = player.GetModPlayer<CyberwarePlayer>();
            if (!TryClassifyRequest(player, state, sessionGeneration, requestId,
                kind, replyTo, out bool shouldProcess)) {
                return;
            }
            if (!shouldProcess) {
                return;
            }

            VictorResultCode code;
            bool loadoutChanged = false;
            if (!state.AllowVictorRequest()) {
                code = VictorResultCode.RateLimited;
            }
            else {
                code = ValidateCommon(player, state, expectedRevision,
                    victorIdentity);
                if (code == VictorResultCode.Success) {
                    code = ExecuteSurgery(player, state, kind, loadoutSlot,
                        inventorySlot, out loadoutChanged);
                }
            }

            VictorRequestResult result = new(sessionGeneration, requestId, kind,
                code, state.LoadoutRevision);
            state.StoreVictorRequestResult(result);
            FinishAuthorityRequest(player, result, replyTo, loadoutChanged);
        }

        private static void ProcessPurchaseRequest(Player player,
            uint sessionGeneration, uint requestId, uint expectedRevision,
            in NetworkNPCIdentity victorIdentity, int loadoutSlot, int itemType,
            int replyTo) {
            CyberwarePlayer state = player.GetModPlayer<CyberwarePlayer>();
            const VictorRequestKind kind = VictorRequestKind.Purchase;
            if (!TryClassifyRequest(player, state, sessionGeneration, requestId,
                kind, replyTo, out bool shouldProcess)) {
                return;
            }
            if (!shouldProcess) {
                return;
            }

            VictorResultCode code;
            if (!state.AllowVictorRequest()) {
                code = VictorResultCode.RateLimited;
            }
            else {
                code = ValidateCommon(player, state, expectedRevision,
                    victorIdentity);
                if (code == VictorResultCode.Success) {
                    code = ExecutePurchase(player, loadoutSlot, itemType);
                }
            }

            VictorRequestResult result = new(sessionGeneration, requestId, kind,
                code, state.LoadoutRevision);
            state.StoreVictorRequestResult(result);
            FinishAuthorityRequest(player, result, replyTo,
                broadcastLoadout: false);
        }

        private static bool TryClassifyRequest(Player player,
            CyberwarePlayer state, uint sessionGeneration, uint requestId,
            VictorRequestKind kind, int replyTo, out bool shouldProcess) {
            shouldProcess = false;
            if (state == null || !state.ProfileInitialized || requestId == 0
                || !VictorProtocol.IsValidKind(kind)) {
                SendSnapshotOnly(player, replyTo);
                return false;
            }

            VictorRequestDisposition disposition = state.ClassifyVictorRequest(
                sessionGeneration, requestId, kind,
                out VictorRequestResult previous);
            if (disposition == VictorRequestDisposition.New) {
                shouldProcess = true;
                return true;
            }
            if (disposition == VictorRequestDisposition.Replay) {
                FinishAuthorityRequest(player, previous, replyTo,
                    broadcastLoadout: false);
                return true;
            }

            VictorResultCode code = disposition switch {
                VictorRequestDisposition.Invalid => VictorResultCode.InvalidSession,
                VictorRequestDisposition.Conflict => VictorResultCode.ConflictingRequest,
                VictorRequestDisposition.Expired => VictorResultCode.ExpiredRequest,
                _ => VictorResultCode.InvalidPayload,
            };
            if (sessionGeneration == 0) {
                SendSnapshotOnly(player, replyTo);
                return false;
            }

            VictorRequestResult result = new(sessionGeneration, requestId, kind,
                code, state.LoadoutRevision);
            FinishAuthorityRequest(player, result, replyTo,
                broadcastLoadout: false);
            return false;
        }

        private static VictorResultCode ValidateCommon(Player player,
            CyberwarePlayer state, uint expectedRevision,
            in NetworkNPCIdentity victorIdentity) {
            if (player?.active != true || player.dead || player.ghost) {
                return VictorResultCode.InvalidPlayer;
            }
            if (expectedRevision == 0
                || expectedRevision != state.LoadoutRevision) {
                return VictorResultCode.StaleLoadout;
            }
            if (!TryResolveVictor(victorIdentity, out NPC victor)
                || !IsFinite(player.Center) || !IsFinite(victor.Center)
                || Vector2.DistanceSquared(player.Center, victor.Center)
                    > MaxVictorDistance * MaxVictorDistance) {
                return VictorResultCode.InvalidVictor;
            }
            return VictorResultCode.Success;
        }

        private static VictorResultCode ExecuteSurgery(Player player,
            CyberwarePlayer state, VictorRequestKind kind, int loadoutSlot,
            int inventorySlot, out bool loadoutChanged) {
            loadoutChanged = false;
            if (loadoutSlot < 0 || loadoutSlot >= CyberwarePlayer.SlotCount) {
                return VictorResultCode.InvalidPayload;
            }

            if (kind == VictorRequestKind.Install) {
                int inventoryCount = Math.Min(Main.InventorySlotsTotal,
                    player.inventory.Length);
                if (inventorySlot < 0 || inventorySlot >= inventoryCount) {
                    return VictorResultCode.InvalidInventoryItem;
                }
                Item source = player.inventory[inventorySlot];
                if (source == null || source.IsAir
                    || source.ModItem is not BaseCyberware cyberware
                    || (int)cyberware.SlotCategory != loadoutSlot
                    || source.stack != 1 || source.maxStack != 1) {
                    return VictorResultCode.InvalidInventoryItem;
                }
                if (!state.CanEquip(source, loadoutSlot)) {
                    return VictorResultCode.CapacityExceeded;
                }

                if (!state.TryInstallAuthority(source, loadoutSlot,
                    out Item previous)) {
                    return VictorResultCode.InvalidPayload;
                }
                player.inventory[inventorySlot] = previous == null || previous.IsAir
                    ? new Item()
                    : previous;
                SyncInventorySlot(player, inventorySlot);
                loadoutChanged = true;
                return VictorResultCode.Success;
            }

            if (kind == VictorRequestKind.Uninstall) {
                Item equipped = state.EquippedCyberwares[loadoutSlot];
                if (equipped == null || equipped.IsAir) {
                    return VictorResultCode.InvalidInventoryItem;
                }
                int destination = FindEmptyMainInventorySlot(player);
                if (destination < 0) {
                    return VictorResultCode.InventoryFull;
                }

                if (!state.TryUninstallAuthority(loadoutSlot,
                    out Item previous)) {
                    return VictorResultCode.InvalidPayload;
                }
                player.inventory[destination] = previous;
                SyncInventorySlot(player, destination);
                loadoutChanged = true;
                return VictorResultCode.Success;
            }

            return VictorResultCode.InvalidPayload;
        }

        private static VictorResultCode ExecutePurchase(Player player,
            int loadoutSlot, int itemType) {
            if (loadoutSlot < 0 || loadoutSlot >= CyberwarePlayer.SlotCount
                || !VictorCatalog.TryGetEntry(itemType,
                    out VictorCatalogEntry entry)
                || entry.SlotIndex != loadoutSlot || entry.Price <= 0L) {
                return VictorResultCode.InvalidPayload;
            }

            int destination = FindEmptyMainInventorySlot(player);
            if (destination < 0) {
                return VictorResultCode.InventoryFull;
            }
            if (!player.CanAfford(entry.Price)) {
                return VictorResultCode.InsufficientFunds;
            }

            Item purchased = new(itemType);
            if (purchased.IsAir || purchased.ModItem is not BaseCyberware cyberware
                || (int)cyberware.SlotCategory != loadoutSlot) {
                return VictorResultCode.InvalidPayload;
            }

            List<WalletSlotSnapshot> walletSnapshot = CaptureWallet(player);
            player.inventory[destination] = purchased;
            bool paid;
            try {
                paid = player.BuyItem(entry.Price);
            } catch {
                RestoreWallet(walletSnapshot);
                return VictorResultCode.InvalidPayload;
            }
            if (!paid) {
                RestoreWallet(walletSnapshot);
                return VictorResultCode.InsufficientFunds;
            }
            SyncWalletChanges(player, walletSnapshot);
            return VictorResultCode.Success;
        }

        private static void FinishAuthorityRequest(Player player,
            in VictorRequestResult result, int replyTo, bool broadcastLoadout) {
            if (Main.netMode == NetmodeID.Server) {
                if (broadcastLoadout) {
                    SendLoadoutSnapshot(player);
                }
                SendRequestResult(player, result, replyTo);
                return;
            }
            if (Main.netMode == NetmodeID.SinglePlayer) {
                CompletePending(result);
            }
        }

        private static void SendRequestResult(Player player,
            in VictorRequestResult result, int toWho) {
            if (Main.netMode != NetmodeID.Server || !result.IsValid
                || toWho < 0 || toWho >= Main.maxPlayers
                || player?.active != true) {
                return;
            }
            CyberwarePlayer state = player.GetModPlayer<CyberwarePlayer>();
            if (!state.ProfileInitialized) {
                return;
            }

            ModPacket packet = NewPacket(CyberwareNetOp.RequestResult);
            WriteResult(packet, result);
            WriteSnapshot(packet, CaptureSnapshot(player, state));
            packet.Send(toWho);
        }

        private static void SendSnapshotOnly(Player player, int replyTo) {
            if (Main.netMode == NetmodeID.Server) {
                SendLoadoutSnapshot(player, replyTo);
            }
        }

        private static bool TryCaptureVictor(int whoAmI,
            out NetworkNPCIdentity identity) {
            identity = default;
            if (whoAmI < 0 || whoAmI >= Main.maxNPCs) {
                return false;
            }
            NPC npc = Main.npc[whoAmI];
            return npc?.active == true
                && npc.type == ModContent.NPCType<Victor>()
                && NetworkNPCIdentity.TryCapture(npc, out identity);
        }

        private static bool TryResolveVictor(in NetworkNPCIdentity identity,
            out NPC victor) {
            victor = null;
            if (!identity.TryResolve(out NPC resolved)
                || resolved.type != ModContent.NPCType<Victor>()
                || !resolved.active || resolved.life <= 0) {
                return false;
            }
            victor = resolved;
            return true;
        }

        private static bool TryResolvePlayer(int whoAmI, bool requireAlive,
            out Player player) {
            player = null;
            if (whoAmI < 0 || whoAmI >= Main.maxPlayers) {
                return false;
            }
            Player candidate = Main.player[whoAmI];
            if (candidate?.active != true || requireAlive
                && (candidate.dead || candidate.ghost)) {
                return false;
            }
            player = candidate;
            return true;
        }

        private static int FindEmptyMainInventorySlot(Player player) {
            int count = Math.Min(MainInventorySlotCount, player.inventory.Length);
            for (int i = 0; i < count; i++) {
                if (player.inventory[i] == null || player.inventory[i].IsAir) {
                    return i;
                }
            }
            return -1;
        }

        private static void SyncInventorySlot(Player player, int inventorySlot) {
            if (Main.netMode != NetmodeID.Server || inventorySlot < 0
                || inventorySlot >= player.inventory.Length) {
                return;
            }
            Item item = player.inventory[inventorySlot] ?? new Item();
            NetMessage.SendData(MessageID.SyncEquipment, -1, -1, null,
                player.whoAmI, PlayerItemSlotID.Inventory0 + inventorySlot,
                item.prefix);
        }

        private static List<WalletSlotSnapshot> CaptureWallet(Player player) {
            List<WalletSlotSnapshot> snapshot = [];
            CaptureContainer(player.inventory, PlayerItemSlotID.Inventory0,
                snapshot);
            CaptureContainer(player.bank?.item, PlayerItemSlotID.Bank1_0,
                snapshot);
            CaptureContainer(player.bank2?.item, PlayerItemSlotID.Bank2_0,
                snapshot);
            CaptureContainer(player.bank3?.item, PlayerItemSlotID.Bank3_0,
                snapshot);
            CaptureContainer(player.bank4?.item, PlayerItemSlotID.Bank4_0,
                snapshot);
            return snapshot;
        }

        private static void CaptureContainer(Item[] container, int networkBase,
            List<WalletSlotSnapshot> snapshot) {
            if (container == null) {
                return;
            }
            for (int i = 0; i < container.Length; i++) {
                Item item = container[i] ?? new Item();
                snapshot.Add(new WalletSlotSnapshot(container, i,
                    networkBase + i, item.Clone()));
            }
        }

        private static void RestoreWallet(
            List<WalletSlotSnapshot> snapshot) {
            for (int i = 0; i < snapshot.Count; i++) {
                WalletSlotSnapshot slot = snapshot[i];
                if (!SameItemState(slot.Container[slot.Index], slot.Item)) {
                    slot.Container[slot.Index] = slot.Item.Clone();
                }
            }
        }

        private static void SyncWalletChanges(Player player,
            List<WalletSlotSnapshot> snapshot) {
            if (Main.netMode != NetmodeID.Server) {
                return;
            }
            for (int i = 0; i < snapshot.Count; i++) {
                WalletSlotSnapshot slot = snapshot[i];
                Item current = slot.Container[slot.Index] ?? new Item();
                if (SameItemState(current, slot.Item)) {
                    continue;
                }
                int toWho = slot.NetworkSlot >= PlayerItemSlotID.Bank1_0
                    ? player.whoAmI
                    : -1;
                NetMessage.SendData(MessageID.SyncEquipment, toWho, -1, null,
                    player.whoAmI, slot.NetworkSlot, current.prefix);
            }
        }

        private static bool SameItemState(Item current, Item previous)
            => (current?.type ?? ItemID.None) == (previous?.type ?? ItemID.None)
                && (current?.stack ?? 0) == (previous?.stack ?? 0)
                && (current?.prefix ?? 0) == (previous?.prefix ?? 0);

        private static bool TryRegisterPending(in VictorRequestToken token,
            VictorRequestKind kind, Action<VictorRequestResult> completion) {
            UpdatePendingRequests();
            if (!token.IsValid || pendingRequests.Count >= MaxPendingRequests
                || pendingRequests.ContainsKey(token.RequestId)) {
                return false;
            }
            pendingRequests[token.RequestId] = new PendingVictorRequest {
                SessionGeneration = token.SessionGeneration,
                Kind = kind,
                CreatedAt = Main.GameUpdateCount,
                Completion = completion,
            };
            return true;
        }

        private static void CancelPending(uint requestId)
            => pendingRequests.Remove(requestId);

        private static void CompletePending(in VictorRequestResult result) {
            if (!result.IsValid
                || !pendingRequests.TryGetValue(result.RequestId,
                    out PendingVictorRequest pending)
                || pending.SessionGeneration != result.RequestSessionGeneration
                || pending.Kind != result.Kind) {
                return;
            }
            pendingRequests.Remove(result.RequestId);
            pending.Completion?.Invoke(result);
        }

        private static CyberwareLoadoutSnapshot CaptureSnapshot(Player player,
            CyberwarePlayer state)
            => new(player.whoAmI, state.SessionGeneration,
                state.LoadoutRevision, state.CaptureLoadoutTypes());

        private static ModPacket NewPacket(CyberwareNetOp operation) {
            ModPacket packet = CWRMod.Instance.GetPacket();
            packet.Write((byte)CWRMessageType.Cyberware);
            packet.Write((byte)operation);
            return packet;
        }

        private static void WriteSnapshot(BinaryWriter writer,
            in CyberwareLoadoutSnapshot snapshot) {
            writer.Write((byte)snapshot.PlayerIndex);
            writer.Write(snapshot.SessionGeneration);
            writer.Write(snapshot.LoadoutRevision);
            WriteTypes(writer, snapshot.ItemTypes);
        }

        private static CyberwareLoadoutSnapshot ReadSnapshot(BinaryReader reader)
            => new(reader.ReadByte(), reader.ReadUInt32(), reader.ReadUInt32(),
                ReadTypes(reader));

        private static void WriteTypes(BinaryWriter writer, ReadOnlySpan<int> types) {
            for (int i = 0; i < CyberwarePlayer.SlotCount; i++) {
                writer.Write(i < types.Length ? types[i] : ItemID.None);
            }
        }

        private static int[] ReadTypes(BinaryReader reader) {
            int[] types = new int[CyberwarePlayer.SlotCount];
            for (int i = 0; i < types.Length; i++) {
                types[i] = reader.ReadInt32();
            }
            return types;
        }

        private static void WriteResult(BinaryWriter writer,
            in VictorRequestResult result) {
            writer.Write(result.RequestSessionGeneration);
            writer.Write(result.RequestId);
            writer.Write((byte)result.Kind);
            writer.Write((byte)result.Code);
            writer.Write(result.AuthorityLoadoutRevision);
        }

        private static VictorRequestResult ReadResult(BinaryReader reader)
            => new(reader.ReadUInt32(), reader.ReadUInt32(),
                (VictorRequestKind)reader.ReadByte(),
                (VictorResultCode)reader.ReadByte(), reader.ReadUInt32());

        private static bool IsRevisionAtLeast(uint candidate, uint baseline)
            => candidate == baseline
                || CyberwarePlayer.IsRevisionNewer(candidate, baseline);

        private static bool IsFinite(Vector2 value)
            => float.IsFinite(value.X) && float.IsFinite(value.Y);
    }

    internal sealed class CyberwareNetSystem : ModSystem
    {
        public override void OnWorldLoad() => CyberwareNet.Reset();

        public override void OnWorldUnload() => CyberwareNet.Reset();

        public override void PostUpdateEverything()
            => CyberwareNet.UpdatePendingRequests();

        public override void Unload() {
            CyberwareNet.Reset();
            VictorCatalog.Reset();
        }
    }
}

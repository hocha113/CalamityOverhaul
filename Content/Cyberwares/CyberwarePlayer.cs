using CalamityOverhaul.Content.Cyberwares.Victors;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.Cyberwares
{
    /// <summary>玩家义体装备与联机权威状态</summary>
    internal sealed class CyberwarePlayer : ModPlayer
    {
        public const int SlotCount = 12;
        public const int FixedMaxCapacity = 20;
        internal const int MaxRecentVictorResults = 64;

        private const int InitialProfileRetryFrames = 120;
        private const int VictorRequestWindowFrames = 60;
        private const int MaxVictorRequestsPerWindow = 16;

        private readonly Dictionary<uint, VictorRequestResult> recentVictorResults = [];
        private readonly Queue<uint> recentVictorOrder = [];

        private uint nextVictorRequestId;
        private uint highestCompletedVictorRequestId;
        private ulong victorRequestWindowStart;
        private int victorRequestWindowCount;
        private int initialProfileRetryTimer;

        public Item[] EquippedCyberwares { get; private set; }
        public int MaxCapacity => FixedMaxCapacity;
        public bool ProfileInitialized { get; private set; }
        public uint SessionGeneration { get; private set; }
        public uint LoadoutRevision { get; private set; }

        public int UsedCapacity {
            get {
                int total = 0;
                for (int i = 0; i < SlotCount; i++) {
                    if (TryGetValidCyberware(EquippedCyberwares[i], i,
                        out BaseCyberware cyber)) {
                        total += cyber.CapacityCost;
                    }
                }
                return Math.Min(total, FixedMaxCapacity + 1);
            }
        }

        public int RemainingCapacity => Math.Max(0, FixedMaxCapacity - UsedCapacity);

        public override void Initialize() {
            EquippedCyberwares = CreateEmptyLoadout();
            ResetAuthorityState(clearLoadout: false);
        }

        public override void OnEnterWorld() {
            bool server = Main.netMode == NetmodeID.Server;
            ResetAuthorityState(clearLoadout: server);

            if (Main.netMode == NetmodeID.SinglePlayer) {
                InitializeAuthorityProfile(CaptureLoadoutTypes(),
                    CyberwareNet.AllocateSessionGeneration());
            }
            else if (Main.netMode == NetmodeID.MultiplayerClient
                && Player.whoAmI == Main.myPlayer) {
                CyberwareNet.SendInitialProfile(this);
            }
        }

        public override void PlayerDisconnect()
            => ResetAuthorityState(clearLoadout: true);

        public override void SyncPlayer(int toWho, int fromWho, bool newPlayer) {
            if (Main.netMode == NetmodeID.Server && ProfileInitialized) {
                CyberwareNet.SendLoadoutSnapshot(Player, toWho);
            }
        }

        public override void PostUpdate() {
            if (Main.netMode == NetmodeID.MultiplayerClient && !ProfileInitialized) {
                RetryInitialProfile();
                return;
            }
            if (!ProfileInitialized) {
                return;
            }

            for (int i = 0; i < SlotCount; i++) {
                if (EquippedCyberwares[i]?.ModItem is BaseCyberware cyber) {
                    cyber.UpdateEquipped(Player);
                }
            }
        }

        public override void PostUpdateEquips() {
            if (!ProfileInitialized) {
                return;
            }

            for (int i = 0; i < SlotCount; i++) {
                if (EquippedCyberwares[i]?.ModItem is BaseCyberware cyber) {
                    cyber.PostUpdateEquipped(Player);
                }
            }
        }

        public bool CanEquip(Item item, int slotIndex) {
            if (!TryGetValidCyberware(item, slotIndex, out BaseCyberware cyber)) {
                return false;
            }

            int currentUsed = UsedCapacity;
            if (TryGetValidCyberware(EquippedCyberwares[slotIndex], slotIndex,
                out BaseCyberware oldCyber)) {
                currentUsed -= oldCyber.CapacityCost;
            }
            return currentUsed >= 0
                && currentUsed + cyber.CapacityCost <= FixedMaxCapacity;
        }

        public bool HasCyberware(int itemType) {
            if (itemType <= ItemID.None || itemType >= ItemLoader.ItemCount) {
                return false;
            }
            for (int i = 0; i < SlotCount; i++) {
                if (EquippedCyberwares[i]?.type == itemType) {
                    return true;
                }
            }
            return false;
        }

        public bool HasCyberware<TCyberware>() where TCyberware : BaseCyberware
            => TryGetCyberware(out TCyberware _);

        public bool TryGetCyberware<TCyberware>(out TCyberware cyberware)
            where TCyberware : BaseCyberware {
            for (int i = 0; i < SlotCount; i++) {
                if (EquippedCyberwares[i]?.ModItem is TCyberware match) {
                    cyberware = match;
                    return true;
                }
            }
            cyberware = null;
            return false;
        }

        public List<int> GetCompatibleItems(int slotIndex) {
            List<int> result = [];
            if (slotIndex < 0 || slotIndex >= SlotCount) {
                return result;
            }

            int count = Math.Min(Main.InventorySlotsTotal, Player.inventory.Length);
            for (int i = 0; i < count; i++) {
                Item item = Player.inventory[i];
                if (TryGetValidCyberware(item, slotIndex, out _)) {
                    result.Add(i);
                }
            }
            return result;
        }

        internal int[] CaptureLoadoutTypes() {
            int[] types = new int[SlotCount];
            for (int i = 0; i < SlotCount; i++) {
                int type = EquippedCyberwares[i]?.type ?? ItemID.None;
                types[i] = type > ItemID.None && type < ItemLoader.ItemCount
                    ? type
                    : ItemID.None;
            }
            return types;
        }

        internal bool InitializeAuthorityProfile(ReadOnlySpan<int> submittedTypes,
            uint sessionGeneration) {
            if (Main.netMode == NetmodeID.MultiplayerClient || ProfileInitialized
                || sessionGeneration == 0) {
                return false;
            }

            Item[] sanitized = SanitizeSubmittedLoadout(submittedTypes);
            ReplaceLoadout(sanitized);
            ProfileInitialized = true;
            SessionGeneration = sessionGeneration;
            LoadoutRevision = 1;
            ResetRequestHistory();
            initialProfileRetryTimer = 0;
            return true;
        }

        internal bool ApplyAuthoritySnapshot(uint sessionGeneration, uint revision,
            ReadOnlySpan<int> itemTypes) {
            if (sessionGeneration == 0 || revision == 0
                || !TryBuildStrictLoadout(itemTypes, out Item[] loadout)) {
                return false;
            }

            if (ProfileInitialized) {
                if (sessionGeneration == SessionGeneration) {
                    if (revision == LoadoutRevision) {
                        return LoadoutMatches(itemTypes);
                    }
                    if (!IsRevisionNewer(revision, LoadoutRevision)) {
                        return true;
                    }
                }
                else if (!IsRevisionNewer(sessionGeneration, SessionGeneration)) {
                    return false;
                }
            }

            bool newSession = !ProfileInitialized
                || sessionGeneration != SessionGeneration;
            ReplaceLoadout(loadout);
            ProfileInitialized = true;
            SessionGeneration = sessionGeneration;
            LoadoutRevision = revision;
            initialProfileRetryTimer = 0;
            if (newSession) {
                CyberwareNet.DropPendingExcept(sessionGeneration);
                ResetRequestHistory();
            }
            return true;
        }

        internal bool TryInstallAuthority(Item item, int slotIndex,
            out Item previousItem) {
            previousItem = null;
            if (Main.netMode == NetmodeID.MultiplayerClient || !ProfileInitialized
                || !CanEquip(item, slotIndex)) {
                return false;
            }

            Item replacement = item.Clone();
            replacement.stack = 1;
            previousItem = EquippedCyberwares[slotIndex];
            InvokeUnequip(previousItem);
            EquippedCyberwares[slotIndex] = replacement;
            InvokeEquip(replacement);
            CommitLoadoutMutation();
            return true;
        }

        internal bool TryUninstallAuthority(int slotIndex, out Item previousItem) {
            previousItem = null;
            if (Main.netMode == NetmodeID.MultiplayerClient || !ProfileInitialized
                || slotIndex < 0 || slotIndex >= SlotCount) {
                return false;
            }

            Item current = EquippedCyberwares[slotIndex];
            if (current == null || current.IsAir) {
                return false;
            }
            previousItem = current;
            InvokeUnequip(current);
            EquippedCyberwares[slotIndex] = new Item();
            CommitLoadoutMutation();
            return true;
        }

        internal bool TryAllocateVictorRequest(VictorRequestKind kind,
            out VictorRequestToken token) {
            token = default;
            if (!ProfileInitialized || SessionGeneration == 0
                || !VictorProtocol.IsValidKind(kind)) {
                return false;
            }

            do {
                nextVictorRequestId++;
            }
            while (nextVictorRequestId == 0
                || recentVictorResults.ContainsKey(nextVictorRequestId));
            token = new VictorRequestToken(SessionGeneration, nextVictorRequestId,
                LoadoutRevision);
            return true;
        }

        internal VictorRequestDisposition ClassifyVictorRequest(uint sessionGeneration,
            uint requestId, VictorRequestKind kind, out VictorRequestResult previous) {
            previous = default;
            if (!ProfileInitialized || sessionGeneration == 0
                || sessionGeneration != SessionGeneration || requestId == 0
                || !VictorProtocol.IsValidKind(kind)) {
                return VictorRequestDisposition.Invalid;
            }
            if (!recentVictorResults.TryGetValue(requestId, out previous)) {
                return highestCompletedVictorRequestId == 0
                    || IsRevisionNewer(requestId, highestCompletedVictorRequestId)
                    ? VictorRequestDisposition.New
                    : VictorRequestDisposition.Expired;
            }
            return previous.Kind == kind
                ? VictorRequestDisposition.Replay
                : VictorRequestDisposition.Conflict;
        }

        internal void StoreVictorRequestResult(in VictorRequestResult result) {
            if (!result.IsValid || result.RequestSessionGeneration != SessionGeneration) {
                return;
            }
            if (recentVictorResults.ContainsKey(result.RequestId)) {
                recentVictorResults[result.RequestId] = result;
                return;
            }

            while (recentVictorResults.Count >= MaxRecentVictorResults
                && recentVictorOrder.TryDequeue(out uint expired)) {
                recentVictorResults.Remove(expired);
            }
            recentVictorResults[result.RequestId] = result;
            recentVictorOrder.Enqueue(result.RequestId);
            if (highestCompletedVictorRequestId == 0
                || IsRevisionNewer(result.RequestId,
                    highestCompletedVictorRequestId)) {
                highestCompletedVictorRequestId = result.RequestId;
            }
        }

        internal bool AllowVictorRequest() {
            ulong now = Main.GameUpdateCount;
            if (now - victorRequestWindowStart >= VictorRequestWindowFrames) {
                victorRequestWindowStart = now;
                victorRequestWindowCount = 0;
            }
            if (victorRequestWindowCount >= MaxVictorRequestsPerWindow) {
                return false;
            }
            victorRequestWindowCount++;
            return true;
        }

        public override void SaveData(TagCompound tag) {
            try {
                tag["CyberMaxCapacity"] = FixedMaxCapacity;
                for (int i = 0; i < SlotCount; i++) {
                    Item item = EquippedCyberwares[i];
                    if (item != null && !item.IsAir) {
                        tag[$"Cyber_{i}"] = ItemIO.Save(item);
                    }
                }
            }
            catch (Exception ex) {
                CWRMod.Instance.Logger.Error($"CyberwarePlayer.SaveData Error: {ex.Message}");
            }
        }

        public override void LoadData(TagCompound tag) {
            Item[] loaded = CreateEmptyLoadout();
            try {
                int used = 0;
                for (int i = 0; i < SlotCount; i++) {
                    if (!tag.TryGet($"Cyber_{i}", out TagCompound itemTag)) {
                        continue;
                    }

                    Item item;
                    try {
                        item = ItemIO.Load(itemTag);
                    }
                    catch {
                        continue;
                    }
                    if (!TryGetValidCyberware(item, i, out BaseCyberware cyber)
                        || used + cyber.CapacityCost > FixedMaxCapacity) {
                        continue;
                    }
                    item.stack = 1;
                    loaded[i] = item;
                    used += cyber.CapacityCost;
                }
            }
            catch (Exception ex) {
                CWRMod.Instance.Logger.Error($"CyberwarePlayer.LoadData Error: {ex.Message}");
            }
            EquippedCyberwares = loaded;
        }

        private void RetryInitialProfile() {
            if (Player.whoAmI != Main.myPlayer || ProfileInitialized) {
                return;
            }
            if (++initialProfileRetryTimer >= InitialProfileRetryFrames) {
                initialProfileRetryTimer = 0;
                CyberwareNet.SendInitialProfile(this);
            }
        }

        private void CommitLoadoutMutation() {
            LoadoutRevision++;
            if (LoadoutRevision == 0) {
                LoadoutRevision = 1;
            }
        }

        private bool LoadoutMatches(ReadOnlySpan<int> itemTypes) {
            if (itemTypes.Length != SlotCount) {
                return false;
            }
            for (int i = 0; i < SlotCount; i++) {
                int currentType = EquippedCyberwares[i]?.type ?? ItemID.None;
                if (currentType != itemTypes[i]) {
                    return false;
                }
            }
            return true;
        }

        private void ReplaceLoadout(Item[] replacement) {
            for (int i = 0; i < SlotCount; i++) {
                Item oldItem = EquippedCyberwares[i] ?? new Item();
                Item newItem = replacement[i] ?? new Item();
                if (oldItem.type == newItem.type) {
                    continue;
                }
                InvokeUnequip(oldItem);
                EquippedCyberwares[i] = newItem;
                InvokeEquip(newItem);
            }
        }

        private void ResetAuthorityState(bool clearLoadout) {
            if (Player?.whoAmI == Main.myPlayer
                && Main.netMode != NetmodeID.Server) {
                CyberwareNet.DropPendingExcept(0);
            }
            ProfileInitialized = false;
            SessionGeneration = 0;
            LoadoutRevision = 0;
            initialProfileRetryTimer = 0;
            victorRequestWindowStart = Main.GameUpdateCount;
            victorRequestWindowCount = 0;
            ResetRequestHistory();
            if (!clearLoadout) {
                return;
            }

            for (int i = 0; i < SlotCount; i++) {
                InvokeUnequip(EquippedCyberwares[i]);
                EquippedCyberwares[i] = new Item();
            }
        }

        private void ResetRequestHistory() {
            nextVictorRequestId = 0;
            highestCompletedVictorRequestId = 0;
            recentVictorResults.Clear();
            recentVictorOrder.Clear();
        }

        private static Item[] SanitizeSubmittedLoadout(ReadOnlySpan<int> submittedTypes) {
            Item[] sanitized = CreateEmptyLoadout();
            int used = 0;
            for (int i = 0; i < SlotCount; i++) {
                int type = i < submittedTypes.Length ? submittedTypes[i] : ItemID.None;
                if (!TryCreateCyberwareItem(type, i, out Item item,
                    out BaseCyberware cyber)
                    || used + cyber.CapacityCost > FixedMaxCapacity) {
                    continue;
                }
                sanitized[i] = item;
                used += cyber.CapacityCost;
            }
            return sanitized;
        }

        private static bool TryBuildStrictLoadout(ReadOnlySpan<int> itemTypes,
            out Item[] loadout) {
            loadout = CreateEmptyLoadout();
            if (itemTypes.Length != SlotCount) {
                return false;
            }

            int used = 0;
            for (int i = 0; i < SlotCount; i++) {
                int type = itemTypes[i];
                if (type == ItemID.None) {
                    continue;
                }
                if (!TryCreateCyberwareItem(type, i, out Item item,
                    out BaseCyberware cyber)
                    || used + cyber.CapacityCost > FixedMaxCapacity) {
                    loadout = null;
                    return false;
                }
                loadout[i] = item;
                used += cyber.CapacityCost;
            }
            return true;
        }

        private static bool TryCreateCyberwareItem(int itemType, int slotIndex,
            out Item item, out BaseCyberware cyberware) {
            item = null;
            cyberware = null;
            if (itemType <= ItemID.None || itemType >= ItemLoader.ItemCount
                || slotIndex < 0 || slotIndex >= SlotCount) {
                return false;
            }

            Item candidate = new(itemType);
            if (!TryGetValidCyberware(candidate, slotIndex,
                out BaseCyberware cyber)) {
                return false;
            }
            item = candidate;
            cyberware = cyber;
            return true;
        }

        private static bool TryGetValidCyberware(Item item, int slotIndex,
            out BaseCyberware cyberware) {
            cyberware = null;
            if (slotIndex < 0 || slotIndex >= SlotCount || item == null
                || item.IsAir || item.type <= ItemID.None
                || item.type >= ItemLoader.ItemCount
                || item.ModItem is not BaseCyberware cyber
                || (int)cyber.SlotCategory != slotIndex
                || cyber.CapacityCost <= 0
                || cyber.CapacityCost > FixedMaxCapacity) {
                return false;
            }
            cyberware = cyber;
            return true;
        }

        private static Item[] CreateEmptyLoadout() {
            Item[] loadout = new Item[SlotCount];
            for (int i = 0; i < SlotCount; i++) {
                loadout[i] = new Item();
            }
            return loadout;
        }

        private void InvokeEquip(Item item) {
            if (item?.ModItem is BaseCyberware cyber) {
                cyber.OnEquip(Player);
            }
        }

        private void InvokeUnequip(Item item) {
            if (item?.ModItem is BaseCyberware cyber) {
                cyber.OnUnequip(Player);
            }
        }

        internal static bool IsRevisionNewer(uint candidate, uint baseline)
            => unchecked((int)(candidate - baseline)) > 0;
    }
}

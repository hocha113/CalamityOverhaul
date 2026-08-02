using InnoVault.Actors;
using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using WeaverGrievancesItem = CalamityOverhaul.Content.Items.Melee.WeaverGrievanceses.WeaverGrievances;

namespace CalamityOverhaul.Content.Items.Melee.WeaverGrievanceses
{
    internal enum WeaverGrievancesManifestationNetOp : byte
    {
        ClaimRequest,
        ClaimPermit,
        ClaimCommit,
        ClaimCancel,
        ClaimResult,
        ClaimedState,
        ClaimDelivery,
        ClaimDeliveryAck,
    }

    internal enum WeaverGrievancesClaimFailure : byte
    {
        None,
        Invalid,
        InventoryFull,
    }

    /// <summary>服务端许可、提交领取，本地播放拔刀</summary>
    internal static class WGManifestationNet
    {
        private const float ClaimRange = WGManifestationActor.InteractDistance + 48f;
        //主背包槽0-49
        private const int MainInventorySlotCount = 50;

        private readonly record struct GrantedItem(int Slot, int Type, int Stack, byte Prefix)
        {
            internal static GrantedItem None => new(-1, ItemID.None, 0, 0);
        }

        private static ModPacket NewPacket(WeaverGrievancesManifestationNetOp operation) {
            ModPacket packet = CWRMod.Instance.GetPacket();
            packet.Write((byte)CWRMessageType.WeaverGrievancesManifestation);
            packet.Write((byte)operation);
            return packet;
        }

        internal static void RequestClaim(WGManifestationActor actor) {
            if (actor == null || !actor.Active) {
                return;
            }

            if (Main.netMode == NetmodeID.SinglePlayer) {
                Player player = Main.LocalPlayer;
                if (TryAuthorize(player, actor, out int token, out WeaverGrievancesClaimFailure failure)) {
                    actor.BeginLocalPull(token);
                }
                else {
                    actor.ApplyClaimResult(0, success: false,
                        inventoryFull: failure == WeaverGrievancesClaimFailure.InventoryFull);
                }
                return;
            }

            if (!VaultUtils.isClient) {
                return;
            }
            ModPacket packet = NewPacket(WeaverGrievancesManifestationNetOp.ClaimRequest);
            WriteActorIdentity(packet, actor);
            packet.Send();
        }

        internal static void CommitClaim(WGManifestationActor actor, int token) {
            if (actor == null || token <= 0) {
                return;
            }

            if (Main.netMode == NetmodeID.SinglePlayer) {
                bool success = TryCommit(Main.LocalPlayer, actor, token,
                    out WeaverGrievancesClaimFailure failure, out GrantedItem grantedItem);
                if (success) {
                    success = TryApplyGrantedItem(grantedItem, out _);
                    Main.LocalPlayer.GetModPlayer<WGAcquisitionPlayer>()
                        .ResolvePendingClaim(token, success);
                    if (!success) {
                        failure = WeaverGrievancesClaimFailure.InventoryFull;
                    }
                }
                actor.ApplyClaimResult(token, success,
                    failure == WeaverGrievancesClaimFailure.InventoryFull);
                return;
            }

            if (!VaultUtils.isClient) {
                return;
            }
            ModPacket packet = NewPacket(WeaverGrievancesManifestationNetOp.ClaimCommit);
            WriteActorIdentity(packet, actor);
            packet.Write(token);
            packet.Send();
        }

        internal static void CancelClaim(WGManifestationActor actor, int token) {
            if (token <= 0) {
                return;
            }
            if (Main.netMode == NetmodeID.SinglePlayer) {
                Main.LocalPlayer.GetModPlayer<WGAcquisitionPlayer>()
                    .CancelPendingClaim(token);
                return;
            }
            if (!VaultUtils.isClient || actor == null) {
                return;
            }
            ModPacket packet = NewPacket(WeaverGrievancesManifestationNetOp.ClaimCancel);
            WriteActorIdentity(packet, actor);
            packet.Write(token);
            packet.Send();
        }

        internal static void SendClaimedState(Player player, int toWho = -1, int fromWho = -1) {
            if (Main.netMode == NetmodeID.SinglePlayer || player == null || !player.active) {
                return;
            }

            ModPacket packet = NewPacket(WeaverGrievancesManifestationNetOp.ClaimedState);
            packet.Write((byte)player.whoAmI);
            packet.Write(player.GetModPlayer<WGAcquisitionPlayer>().Claimed);
            if (VaultUtils.isClient) {
                if (player.whoAmI == Main.myPlayer) {
                    packet.Send();
                }
            }
            else {
                packet.Send(toWho, fromWho);
            }
        }

        internal static void NetHandle(CWRMessageType type, BinaryReader reader, int whoAmI) {
            if (type != CWRMessageType.WeaverGrievancesManifestation) {
                return;
            }

            WeaverGrievancesManifestationNetOp operation
                = (WeaverGrievancesManifestationNetOp)reader.ReadByte();
            switch (operation) {
                case WeaverGrievancesManifestationNetOp.ClaimRequest:
                    if (VaultUtils.isServer) {
                        HandleClaimRequest(reader, whoAmI);
                    }
                    break;
                case WeaverGrievancesManifestationNetOp.ClaimPermit:
                    if (VaultUtils.isClient) {
                        HandleClaimPermit(reader);
                    }
                    break;
                case WeaverGrievancesManifestationNetOp.ClaimCommit:
                    if (VaultUtils.isServer) {
                        HandleClaimCommit(reader, whoAmI);
                    }
                    break;
                case WeaverGrievancesManifestationNetOp.ClaimCancel:
                    if (VaultUtils.isServer) {
                        HandleClaimCancel(reader, whoAmI);
                    }
                    break;
                case WeaverGrievancesManifestationNetOp.ClaimResult:
                    if (VaultUtils.isClient) {
                        HandleClaimResult(reader);
                    }
                    break;
                case WeaverGrievancesManifestationNetOp.ClaimedState:
                    HandleClaimedState(reader, whoAmI);
                    break;
                case WeaverGrievancesManifestationNetOp.ClaimDelivery:
                    if (VaultUtils.isClient) {
                        HandleClaimDelivery(reader);
                    }
                    break;
                case WeaverGrievancesManifestationNetOp.ClaimDeliveryAck:
                    if (VaultUtils.isServer) {
                        HandleClaimDeliveryAck(reader, whoAmI);
                    }
                    break;
            }
        }

        private static void HandleClaimRequest(BinaryReader reader, int whoAmI) {
            ReadActorIdentity(reader, out int slot, out ushort generation);
            Player player = ResolvePlayer(whoAmI);
            if (player == null || !WGManifestationSystem.TryResolveActor(
                slot, generation, out WGManifestationActor actor)) {
                SendClaimResult(whoAmI, slot, generation, 0, false,
                    WeaverGrievancesClaimFailure.Invalid, GrantedItem.None);
                return;
            }

            if (!TryAuthorize(player, actor, out int token, out WeaverGrievancesClaimFailure failure)) {
                SendClaimResult(whoAmI, slot, generation, 0, false, failure, GrantedItem.None);
                return;
            }

            ModPacket packet = NewPacket(WeaverGrievancesManifestationNetOp.ClaimPermit);
            WriteActorIdentity(packet, actor);
            packet.Write(token);
            packet.Send(whoAmI);
        }

        private static void HandleClaimPermit(BinaryReader reader) {
            ReadActorIdentity(reader, out int slot, out ushort generation);
            int token = reader.ReadInt32();
            if (WGManifestationSystem.TryResolveActor(slot, generation,
                out WGManifestationActor actor) && actor.BeginLocalPull(token)) {
                return;
            }

            if (token > 0) {
                ModPacket packet = NewPacket(WeaverGrievancesManifestationNetOp.ClaimCancel);
                packet.Write((ushort)slot);
                packet.Write(generation);
                packet.Write(token);
                packet.Send();
            }
        }

        private static void HandleClaimCommit(BinaryReader reader, int whoAmI) {
            ReadActorIdentity(reader, out int slot, out ushort generation);
            int token = reader.ReadInt32();
            Player player = ResolvePlayer(whoAmI);
            WGManifestationActor actor = null;
            if (player != null) {
                WGManifestationSystem.TryResolveActor(slot, generation, out actor);
            }

            WeaverGrievancesClaimFailure failure = WeaverGrievancesClaimFailure.Invalid;
            GrantedItem grantedItem = GrantedItem.None;
            bool success = actor != null
                && TryCommit(player, actor, token, out failure, out grantedItem);
            if (actor == null) {
                failure = WeaverGrievancesClaimFailure.Invalid;
                player?.GetModPlayer<WGAcquisitionPlayer>().CancelPendingClaim(token);
            }
            if (!success) {
                SendClaimResult(whoAmI, slot, generation, token, false, failure,
                    GrantedItem.None);
            }
            else if (grantedItem.Type == ItemID.None) {
                player.GetModPlayer<WGAcquisitionPlayer>()
                    .ResolvePendingClaim(token, accepted: true);
                SendClaimResult(whoAmI, slot, generation, token, true,
                    WeaverGrievancesClaimFailure.None, GrantedItem.None);
                SendClaimedState(player);
            }
            else {
                SendClaimDelivery(whoAmI, slot, generation, token, grantedItem);
            }
        }

        private static void HandleClaimCancel(BinaryReader reader, int whoAmI) {
            ReadActorIdentity(reader, out _, out _);
            int token = reader.ReadInt32();
            ResolvePlayer(whoAmI)?.GetModPlayer<WGAcquisitionPlayer>()
                .CancelPendingClaim(token);
        }

        private static void HandleClaimResult(BinaryReader reader) {
            ReadActorIdentity(reader, out int slot, out ushort generation);
            int token = reader.ReadInt32();
            bool success = reader.ReadBoolean();
            WeaverGrievancesClaimFailure failure = (WeaverGrievancesClaimFailure)reader.ReadByte();
            GrantedItem grantedItem = new(reader.ReadInt16(), reader.ReadInt32(),
                reader.ReadInt32(), reader.ReadByte());
            if (success) {
                TryApplyGrantedItem(grantedItem, out _);
            }
            if (WGManifestationSystem.TryResolveActor(slot, generation,
                out WGManifestationActor actor)) {
                actor.ApplyClaimResult(token, success,
                    failure == WeaverGrievancesClaimFailure.InventoryFull);
            }
        }

        private static void HandleClaimDelivery(BinaryReader reader) {
            ReadActorIdentity(reader, out int slot, out ushort generation);
            int token = reader.ReadInt32();
            GrantedItem grantedItem = ReadGrantedItem(reader);
            bool delivered = TryApplyGrantedItem(grantedItem, out int actualSlot);

            ModPacket packet = NewPacket(WeaverGrievancesManifestationNetOp.ClaimDeliveryAck);
            packet.Write((ushort)slot);
            packet.Write(generation);
            packet.Write(token);
            packet.Write(delivered);
            packet.Write((short)actualSlot);
            packet.Send();
        }

        private static void HandleClaimDeliveryAck(BinaryReader reader, int whoAmI) {
            ReadActorIdentity(reader, out int slot, out ushort generation);
            int token = reader.ReadInt32();
            bool delivered = reader.ReadBoolean();
            int actualSlot = reader.ReadInt16();
            Player player = ResolvePlayer(whoAmI);
            WGAcquisitionPlayer acquisition
                = player?.GetModPlayer<WGAcquisitionPlayer>();
            bool valid = player != null && acquisition?.MatchesPendingClaim(token) == true
                && WGManifestationSystem.TryResolveActor(slot, generation, out _);

            if (!valid || !delivered) {
                acquisition?.ResolvePendingClaim(token, accepted: false);
                SendClaimResult(whoAmI, slot, generation, token, false,
                    delivered ? WeaverGrievancesClaimFailure.Invalid
                        : WeaverGrievancesClaimFailure.InventoryFull,
                    GrantedItem.None);
                return;
            }

            if (actualSlot >= 0) {
                int count = System.Math.Min(MainInventorySlotCount, player.inventory.Length);
                if (actualSlot >= count) {
                    acquisition.ResolvePendingClaim(token, accepted: false);
                    SendClaimResult(whoAmI, slot, generation, token, false,
                        WeaverGrievancesClaimFailure.Invalid, GrantedItem.None);
                    return;
                }
                player.inventory[actualSlot] = new Item(ModContent.ItemType<WeaverGrievancesItem>());
            }

            acquisition.ResolvePendingClaim(token, accepted: true);
            SendClaimResult(whoAmI, slot, generation, token, true,
                WeaverGrievancesClaimFailure.None, GrantedItem.None);
            SendClaimedState(player);
        }

        private static void HandleClaimedState(BinaryReader reader, int whoAmI) {
            int playerIndex = reader.ReadByte();
            bool claimed = reader.ReadBoolean();
            if (playerIndex < 0 || playerIndex >= Main.maxPlayers) {
                return;
            }

            if (VaultUtils.isServer) {
                if (playerIndex != whoAmI || !claimed) {
                    return;
                }
                Player player = ResolvePlayer(whoAmI);
                if (player == null) {
                    return;
                }
                player.GetModPlayer<WGAcquisitionPlayer>().ApplySyncedClaimed(true);
                SendClaimedState(player, -1, whoAmI);
                return;
            }

            if (VaultUtils.isClient) {
                Player player = Main.player[playerIndex];
                if (player != null && player.active) {
                    player.GetModPlayer<WGAcquisitionPlayer>().ApplySyncedClaimed(claimed);
                }
            }
        }

        private static bool TryAuthorize(Player player, WGManifestationActor actor,
            out int token, out WeaverGrievancesClaimFailure failure) {
            token = 0;
            failure = ValidateActorAndPlayer(player, actor);
            if (failure != WeaverGrievancesClaimFailure.None) {
                return false;
            }

            WGAcquisitionPlayer acquisition
                = player.GetModPlayer<WGAcquisitionPlayer>();
            if (!acquisition.CanRequestClaim) {
                failure = WeaverGrievancesClaimFailure.Invalid;
                return false;
            }
            if (WGAcquisitionPlayer.HasWeaponInPersonalStorage(player)) {
                acquisition.TryMarkClaimed();
                SendClaimedState(player);
                failure = WeaverGrievancesClaimFailure.Invalid;
                return false;
            }
            Item weapon = new(ModContent.ItemType<WeaverGrievancesItem>());
            if (!player.ItemSpace(weapon).CanTakeItemToPersonalInventory) {
                failure = WeaverGrievancesClaimFailure.InventoryFull;
                return false;
            }
            if (!acquisition.TryBeginPendingClaim(out token)) {
                failure = WeaverGrievancesClaimFailure.Invalid;
                return false;
            }
            return true;
        }

        private static bool TryCommit(Player player, WGManifestationActor actor, int token,
            out WeaverGrievancesClaimFailure failure, out GrantedItem grantedItem) {
            grantedItem = GrantedItem.None;
            WGAcquisitionPlayer acquisition
                = player?.GetModPlayer<WGAcquisitionPlayer>();
            failure = ValidateActorAndPlayer(player, actor);
            if (failure == WeaverGrievancesClaimFailure.None
                && (acquisition == null || !acquisition.MatchesPendingClaim(token))) {
                failure = WeaverGrievancesClaimFailure.Invalid;
            }
            if (failure != WeaverGrievancesClaimFailure.None) {
                acquisition?.CancelPendingClaim(token);
                return false;
            }

            if (WGAcquisitionPlayer.HasWeaponInPersonalStorage(player)) {
                return true;
            }

            int itemType = ModContent.ItemType<WeaverGrievancesItem>();
            int inventorySlot = FindEmptyInventorySlot(player);
            if (inventorySlot < 0) {
                acquisition.ResolvePendingClaim(token, accepted: false);
                failure = WeaverGrievancesClaimFailure.InventoryFull;
                return false;
            }

            Item item = new(itemType);
            grantedItem = new GrantedItem(inventorySlot, item.type, item.stack, (byte)item.prefix);
            return true;
        }

        private static WeaverGrievancesClaimFailure ValidateActorAndPlayer(Player player,
            WGManifestationActor actor) {
            if (player == null || !player.active || player.dead || actor == null || !actor.Active
                || !actor.IsPlanted || !WGManifestationSystem.Unlocked
                || player.Center.DistanceSQ(actor.SwordAnchor) > ClaimRange * ClaimRange) {
                return WeaverGrievancesClaimFailure.Invalid;
            }

            WGAcquisitionPlayer acquisition
                = player.GetModPlayer<WGAcquisitionPlayer>();
            if (acquisition.Claimed) {
                return WeaverGrievancesClaimFailure.Invalid;
            }
            return WeaverGrievancesClaimFailure.None;
        }

        private static int FindEmptyInventorySlot(Player player) {
            int count = System.Math.Min(MainInventorySlotCount, player.inventory.Length);
            for (int slot = 0; slot < count; slot++) {
                if (player.inventory[slot] == null || player.inventory[slot].IsAir) {
                    return slot;
                }
            }
            return -1;
        }

        private static bool TryApplyGrantedItem(GrantedItem grantedItem, out int actualSlot) {
            Player player = Main.LocalPlayer;
            actualSlot = -1;
            if (grantedItem.Type == ItemID.None) {
                return true;
            }
            if (player == null || !player.active || grantedItem.Slot < 0
                || grantedItem.Slot >= System.Math.Min(MainInventorySlotCount, player.inventory.Length)
                || grantedItem.Type != ModContent.ItemType<WeaverGrievancesItem>()
                || grantedItem.Stack != 1) {
                CWRMod.Instance.Logger.Error("[WeaverGrievancesManifestation] Invalid item grant payload");
                return false;
            }
            if (WGAcquisitionPlayer.HasWeaponInPersonalStorage(player)) {
                return true;
            }

            actualSlot = grantedItem.Slot;
            if (player.inventory[actualSlot] != null && !player.inventory[actualSlot].IsAir) {
                actualSlot = FindEmptyInventorySlot(player);
                if (actualSlot < 0) {
                    return false;
                }
            }

            Item item = new(grantedItem.Type) {
                stack = grantedItem.Stack,
            };
            if (grantedItem.Prefix > 0) {
                item.Prefix(grantedItem.Prefix);
            }
            player.inventory[actualSlot] = item;
            if (VaultUtils.isClient) {
                NetMessage.SendData(MessageID.SyncEquipment, -1, -1, null, player.whoAmI,
                    PlayerItemSlotID.Inventory0 + actualSlot, item.prefix);
            }
            return true;
        }

        private static void SendClaimDelivery(int toWho, int slot, ushort generation,
            int token, GrantedItem grantedItem) {
            ModPacket packet = NewPacket(WeaverGrievancesManifestationNetOp.ClaimDelivery);
            packet.Write((ushort)slot);
            packet.Write(generation);
            packet.Write(token);
            WriteGrantedItem(packet, grantedItem);
            packet.Send(toWho);
        }

        private static void SendClaimResult(int toWho, int slot, ushort generation, int token,
            bool success, WeaverGrievancesClaimFailure failure, GrantedItem grantedItem) {
            if (!VaultUtils.isServer) {
                return;
            }
            ModPacket packet = NewPacket(WeaverGrievancesManifestationNetOp.ClaimResult);
            packet.Write((ushort)slot);
            packet.Write(generation);
            packet.Write(token);
            packet.Write(success);
            packet.Write((byte)failure);
            WriteGrantedItem(packet, grantedItem);
            packet.Send(toWho);
        }

        private static void WriteGrantedItem(BinaryWriter writer, GrantedItem grantedItem) {
            writer.Write((short)grantedItem.Slot);
            writer.Write(grantedItem.Type);
            writer.Write(grantedItem.Stack);
            writer.Write(grantedItem.Prefix);
        }

        private static GrantedItem ReadGrantedItem(BinaryReader reader)
            => new(reader.ReadInt16(), reader.ReadInt32(), reader.ReadInt32(), reader.ReadByte());

        private static void WriteActorIdentity(BinaryWriter writer,
            WGManifestationActor actor) {
            writer.Write((ushort)actor.WhoAmI);
            writer.Write(actor.Generation);
        }

        private static void ReadActorIdentity(BinaryReader reader, out int slot, out ushort generation) {
            slot = reader.ReadUInt16();
            generation = reader.ReadUInt16();
        }

        private static Player ResolvePlayer(int whoAmI) {
            if (whoAmI < 0 || whoAmI >= Main.maxPlayers) {
                return null;
            }
            Player player = Main.player[whoAmI];
            return player != null && player.active && !player.dead ? player : null;
        }
    }
}

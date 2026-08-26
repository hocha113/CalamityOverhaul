using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee.Arbiters
{
    internal enum ArbiterManifestationNetOp : byte
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

    internal enum ArbiterClaimFailure : byte
    {
        None,
        Invalid,
        InventoryFull,
    }

    /// <summary>服务端许可、提交领取，本地播放拔斧(镜像 WGManifestationNet)，类本身即信道</summary>
    internal class ArbiterManifestationNet : CWRNetChannel
    {
        private const float ClaimRange = ArbiterManifestationActor.InteractDistance + 48f;
        //主背包槽0-49
        private const int MainInventorySlotCount = 50;

        private readonly record struct GrantedItem(int Slot, int Type, int Stack, byte Prefix)
        {
            internal static GrantedItem None => new(-1, ItemID.None, 0, 0);
        }

        private static ModPacket NewPacket(ArbiterManifestationNetOp operation) {
            ModPacket packet = CWRNetWork.GetPacket<ArbiterManifestationNet>();
            packet.Write((byte)operation);
            return packet;
        }

        internal static void RequestClaim(ArbiterManifestationActor actor) {
            if (actor == null || !actor.Active) {
                return;
            }

            if (Main.netMode == NetmodeID.SinglePlayer) {
                Player player = Main.LocalPlayer;
                if (TryAuthorize(player, actor, out int token, out ArbiterClaimFailure failure)) {
                    actor.BeginLocalPull(token);
                }
                else {
                    actor.ApplyClaimResult(0, success: false,
                        inventoryFull: failure == ArbiterClaimFailure.InventoryFull);
                }
                return;
            }

            if (!VaultUtils.isClient) {
                return;
            }
            ModPacket packet = NewPacket(ArbiterManifestationNetOp.ClaimRequest);
            WriteActorIdentity(packet, actor);
            packet.Send();
        }

        internal static void CommitClaim(ArbiterManifestationActor actor, int token) {
            if (actor == null || token <= 0) {
                return;
            }

            if (Main.netMode == NetmodeID.SinglePlayer) {
                bool success = TryCommit(Main.LocalPlayer, actor, token,
                    out ArbiterClaimFailure failure, out GrantedItem grantedItem);
                if (success) {
                    success = TryApplyGrantedItem(grantedItem, out _);
                    Main.LocalPlayer.GetModPlayer<ArbiterAcquisitionPlayer>()
                        .ResolvePendingClaim(token, success);
                    if (!success) {
                        failure = ArbiterClaimFailure.InventoryFull;
                    }
                }
                actor.ApplyClaimResult(token, success,
                    failure == ArbiterClaimFailure.InventoryFull);
                return;
            }

            if (!VaultUtils.isClient) {
                return;
            }
            ModPacket packet = NewPacket(ArbiterManifestationNetOp.ClaimCommit);
            WriteActorIdentity(packet, actor);
            packet.Write(token);
            packet.Send();
        }

        internal static void CancelClaim(ArbiterManifestationActor actor, int token) {
            if (token <= 0) {
                return;
            }
            if (Main.netMode == NetmodeID.SinglePlayer) {
                Main.LocalPlayer.GetModPlayer<ArbiterAcquisitionPlayer>()
                    .CancelPendingClaim(token);
                return;
            }
            if (!VaultUtils.isClient || actor == null) {
                return;
            }
            ModPacket packet = NewPacket(ArbiterManifestationNetOp.ClaimCancel);
            WriteActorIdentity(packet, actor);
            packet.Write(token);
            packet.Send();
        }

        internal static void SendClaimedState(Player player, int toWho = -1, int fromWho = -1) {
            if (Main.netMode == NetmodeID.SinglePlayer || player == null || !player.active) {
                return;
            }

            ModPacket packet = NewPacket(ArbiterManifestationNetOp.ClaimedState);
            packet.Write((byte)player.whoAmI);
            packet.Write(player.GetModPlayer<ArbiterAcquisitionPlayer>().Claimed);
            if (VaultUtils.isClient) {
                if (player.whoAmI == Main.myPlayer) {
                    packet.Send();
                }
            }
            else {
                packet.Send(toWho, fromWho);
            }
        }

        public override void Receive(BinaryReader reader, int whoAmI) {
            ArbiterManifestationNetOp operation
                = (ArbiterManifestationNetOp)reader.ReadByte();
            switch (operation) {
                case ArbiterManifestationNetOp.ClaimRequest:
                    if (VaultUtils.isServer) {
                        HandleClaimRequest(reader, whoAmI);
                    }
                    break;
                case ArbiterManifestationNetOp.ClaimPermit:
                    if (VaultUtils.isClient) {
                        HandleClaimPermit(reader);
                    }
                    break;
                case ArbiterManifestationNetOp.ClaimCommit:
                    if (VaultUtils.isServer) {
                        HandleClaimCommit(reader, whoAmI);
                    }
                    break;
                case ArbiterManifestationNetOp.ClaimCancel:
                    if (VaultUtils.isServer) {
                        HandleClaimCancel(reader, whoAmI);
                    }
                    break;
                case ArbiterManifestationNetOp.ClaimResult:
                    if (VaultUtils.isClient) {
                        HandleClaimResult(reader);
                    }
                    break;
                case ArbiterManifestationNetOp.ClaimedState:
                    HandleClaimedState(reader, whoAmI);
                    break;
                case ArbiterManifestationNetOp.ClaimDelivery:
                    if (VaultUtils.isClient) {
                        HandleClaimDelivery(reader);
                    }
                    break;
                case ArbiterManifestationNetOp.ClaimDeliveryAck:
                    if (VaultUtils.isServer) {
                        HandleClaimDeliveryAck(reader, whoAmI);
                    }
                    break;
            }
        }

        private static void HandleClaimRequest(BinaryReader reader, int whoAmI) {
            ReadActorIdentity(reader, out int slot, out ushort generation);
            Player player = ResolvePlayer(whoAmI);
            if (player == null || !ArbiterManifestationSystem.TryResolveActor(
                slot, generation, out ArbiterManifestationActor actor)) {
                SendClaimResult(whoAmI, slot, generation, 0, false,
                    ArbiterClaimFailure.Invalid, GrantedItem.None);
                return;
            }

            if (!TryAuthorize(player, actor, out int token, out ArbiterClaimFailure failure)) {
                SendClaimResult(whoAmI, slot, generation, 0, false, failure, GrantedItem.None);
                return;
            }

            ModPacket packet = NewPacket(ArbiterManifestationNetOp.ClaimPermit);
            WriteActorIdentity(packet, actor);
            packet.Write(token);
            packet.Send(whoAmI);
        }

        private static void HandleClaimPermit(BinaryReader reader) {
            ReadActorIdentity(reader, out int slot, out ushort generation);
            int token = reader.ReadInt32();
            if (ArbiterManifestationSystem.TryResolveActor(slot, generation,
                out ArbiterManifestationActor actor) && actor.BeginLocalPull(token)) {
                return;
            }

            if (token > 0) {
                ModPacket packet = NewPacket(ArbiterManifestationNetOp.ClaimCancel);
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
            ArbiterManifestationActor actor = null;
            if (player != null) {
                ArbiterManifestationSystem.TryResolveActor(slot, generation, out actor);
            }

            ArbiterClaimFailure failure = ArbiterClaimFailure.Invalid;
            GrantedItem grantedItem = GrantedItem.None;
            bool success = actor != null
                && TryCommit(player, actor, token, out failure, out grantedItem);
            if (actor == null) {
                failure = ArbiterClaimFailure.Invalid;
                player?.GetModPlayer<ArbiterAcquisitionPlayer>().CancelPendingClaim(token);
            }
            if (!success) {
                SendClaimResult(whoAmI, slot, generation, token, false, failure,
                    GrantedItem.None);
            }
            else if (grantedItem.Type == ItemID.None) {
                player.GetModPlayer<ArbiterAcquisitionPlayer>()
                    .ResolvePendingClaim(token, accepted: true);
                SendClaimResult(whoAmI, slot, generation, token, true,
                    ArbiterClaimFailure.None, GrantedItem.None);
                SendClaimedState(player);
            }
            else {
                SendClaimDelivery(whoAmI, slot, generation, token, grantedItem);
            }
        }

        private static void HandleClaimCancel(BinaryReader reader, int whoAmI) {
            ReadActorIdentity(reader, out _, out _);
            int token = reader.ReadInt32();
            ResolvePlayer(whoAmI)?.GetModPlayer<ArbiterAcquisitionPlayer>()
                .CancelPendingClaim(token);
        }

        private static void HandleClaimResult(BinaryReader reader) {
            ReadActorIdentity(reader, out int slot, out ushort generation);
            int token = reader.ReadInt32();
            bool success = reader.ReadBoolean();
            ArbiterClaimFailure failure = (ArbiterClaimFailure)reader.ReadByte();
            GrantedItem grantedItem = new(reader.ReadInt16(), reader.ReadInt32(),
                reader.ReadInt32(), reader.ReadByte());
            if (success) {
                TryApplyGrantedItem(grantedItem, out _);
            }
            if (ArbiterManifestationSystem.TryResolveActor(slot, generation,
                out ArbiterManifestationActor actor)) {
                actor.ApplyClaimResult(token, success,
                    failure == ArbiterClaimFailure.InventoryFull);
            }
        }

        private static void HandleClaimDelivery(BinaryReader reader) {
            ReadActorIdentity(reader, out int slot, out ushort generation);
            int token = reader.ReadInt32();
            GrantedItem grantedItem = ReadGrantedItem(reader);
            bool delivered = TryApplyGrantedItem(grantedItem, out int actualSlot);

            ModPacket packet = NewPacket(ArbiterManifestationNetOp.ClaimDeliveryAck);
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
            ArbiterAcquisitionPlayer acquisition
                = player?.GetModPlayer<ArbiterAcquisitionPlayer>();
            bool valid = player != null && acquisition?.MatchesPendingClaim(token) == true
                && ArbiterManifestationSystem.TryResolveActor(slot, generation, out _);

            if (!valid || !delivered) {
                acquisition?.ResolvePendingClaim(token, accepted: false);
                SendClaimResult(whoAmI, slot, generation, token, false,
                    delivered ? ArbiterClaimFailure.Invalid
                        : ArbiterClaimFailure.InventoryFull,
                    GrantedItem.None);
                return;
            }

            if (actualSlot >= 0) {
                int count = System.Math.Min(MainInventorySlotCount, player.inventory.Length);
                if (actualSlot >= count) {
                    acquisition.ResolvePendingClaim(token, accepted: false);
                    SendClaimResult(whoAmI, slot, generation, token, false,
                        ArbiterClaimFailure.Invalid, GrantedItem.None);
                    return;
                }
                player.inventory[actualSlot] = new Item(ModContent.ItemType<Arbiter>());
            }

            acquisition.ResolvePendingClaim(token, accepted: true);
            SendClaimResult(whoAmI, slot, generation, token, true,
                ArbiterClaimFailure.None, GrantedItem.None);
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
                player.GetModPlayer<ArbiterAcquisitionPlayer>().ApplySyncedClaimed(true);
                SendClaimedState(player, -1, whoAmI);
                return;
            }

            if (VaultUtils.isClient) {
                Player player = Main.player[playerIndex];
                if (player != null && player.active) {
                    player.GetModPlayer<ArbiterAcquisitionPlayer>().ApplySyncedClaimed(claimed);
                }
            }
        }

        private static bool TryAuthorize(Player player, ArbiterManifestationActor actor,
            out int token, out ArbiterClaimFailure failure) {
            token = 0;
            failure = ValidateActorAndPlayer(player, actor);
            if (failure != ArbiterClaimFailure.None) {
                return false;
            }

            ArbiterAcquisitionPlayer acquisition
                = player.GetModPlayer<ArbiterAcquisitionPlayer>();
            if (!acquisition.CanRequestClaim) {
                failure = ArbiterClaimFailure.Invalid;
                return false;
            }
            if (ArbiterAcquisitionPlayer.HasWeaponInPersonalStorage(player)) {
                acquisition.TryMarkClaimed();
                SendClaimedState(player);
                failure = ArbiterClaimFailure.Invalid;
                return false;
            }
            Item weapon = new(ModContent.ItemType<Arbiter>());
            if (!player.ItemSpace(weapon).CanTakeItemToPersonalInventory) {
                failure = ArbiterClaimFailure.InventoryFull;
                return false;
            }
            if (!acquisition.TryBeginPendingClaim(out token)) {
                failure = ArbiterClaimFailure.Invalid;
                return false;
            }
            return true;
        }

        private static bool TryCommit(Player player, ArbiterManifestationActor actor, int token,
            out ArbiterClaimFailure failure, out GrantedItem grantedItem) {
            grantedItem = GrantedItem.None;
            ArbiterAcquisitionPlayer acquisition
                = player?.GetModPlayer<ArbiterAcquisitionPlayer>();
            failure = ValidateActorAndPlayer(player, actor);
            if (failure == ArbiterClaimFailure.None
                && (acquisition == null || !acquisition.MatchesPendingClaim(token))) {
                failure = ArbiterClaimFailure.Invalid;
            }
            if (failure != ArbiterClaimFailure.None) {
                acquisition?.CancelPendingClaim(token);
                return false;
            }

            if (ArbiterAcquisitionPlayer.HasWeaponInPersonalStorage(player)) {
                return true;
            }

            int itemType = ModContent.ItemType<Arbiter>();
            int inventorySlot = FindEmptyInventorySlot(player);
            if (inventorySlot < 0) {
                acquisition.ResolvePendingClaim(token, accepted: false);
                failure = ArbiterClaimFailure.InventoryFull;
                return false;
            }

            Item item = new(itemType);
            grantedItem = new GrantedItem(inventorySlot, item.type, item.stack, (byte)item.prefix);
            return true;
        }

        private static ArbiterClaimFailure ValidateActorAndPlayer(Player player,
            ArbiterManifestationActor actor) {
            if (player == null || !player.active || player.dead || actor == null || !actor.Active
                || !actor.IsPlanted || !ArbiterManifestationSystem.Unlocked
                || player.Center.DistanceSQ(actor.AxeAnchor) > ClaimRange * ClaimRange) {
                return ArbiterClaimFailure.Invalid;
            }

            ArbiterAcquisitionPlayer acquisition
                = player.GetModPlayer<ArbiterAcquisitionPlayer>();
            if (acquisition.Claimed) {
                return ArbiterClaimFailure.Invalid;
            }
            return ArbiterClaimFailure.None;
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
                || grantedItem.Type != ModContent.ItemType<Arbiter>()
                || grantedItem.Stack != 1) {
                CWRMod.Instance.Logger.Error("[ArbiterManifestation] Invalid item grant payload");
                return false;
            }
            if (ArbiterAcquisitionPlayer.HasWeaponInPersonalStorage(player)) {
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
            ModPacket packet = NewPacket(ArbiterManifestationNetOp.ClaimDelivery);
            packet.Write((ushort)slot);
            packet.Write(generation);
            packet.Write(token);
            WriteGrantedItem(packet, grantedItem);
            packet.Send(toWho);
        }

        private static void SendClaimResult(int toWho, int slot, ushort generation, int token,
            bool success, ArbiterClaimFailure failure, GrantedItem grantedItem) {
            if (!VaultUtils.isServer) {
                return;
            }
            ModPacket packet = NewPacket(ArbiterManifestationNetOp.ClaimResult);
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
            ArbiterManifestationActor actor) {
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

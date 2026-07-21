using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.Wraiths.Core
{
    /// <summary>载体句柄；Store 为 null 即无效</summary>
    public readonly struct WraithVesselHandle(Item item, WraithProgressStore store)
    {
        public readonly Item Item = item;
        public readonly WraithProgressStore Store = store;
        public bool IsValid => Store != null && Item != null && !Item.IsAir;
    }

    /// <summary>
    /// 载体解析缝，框架不直接引鬼切；载体方 SetupData 注册。<br/>
    /// ResolveHeld=手中；ResolveCarried=随身（背包躲不掉躁动）
    /// </summary>
    public static class WraithVessels
    {
        /// <summary>手持解析器</summary>
        private static readonly List<Func<Player, WraithVesselHandle>> heldResolvers = [];
        /// <summary>随身解析器</summary>
        private static readonly List<Func<Player, WraithVesselHandle>> carriedResolvers = [];

        public static void Register(Func<Player, WraithVesselHandle> heldResolver, Func<Player, WraithVesselHandle> carriedResolver) {
            if (heldResolver != null) {
                heldResolvers.Add(heldResolver);
            }
            if (carriedResolver != null) {
                carriedResolvers.Add(carriedResolver);
            }
        }

        public static void Clear() {
            heldResolvers.Clear();
            carriedResolvers.Clear();
        }

        /// <summary>手中载体</summary>
        public static WraithVesselHandle ResolveHeld(Player player) => Resolve(heldResolvers, player);

        /// <summary>随身载体，手中优先</summary>
        public static WraithVesselHandle ResolveCarried(Player player) => Resolve(carriedResolvers, player);

        /// <summary>簿面写入后显式推持有槽同步；单人/服务器无操作</summary>
        public static void SyncSlot(Player player, Item item) {
            if (!VaultUtils.isClient || player == null || item == null || item.IsAir
                || player.whoAmI != Main.myPlayer) {
                return;
            }
            int slotId = -1;
            if (ReferenceEquals(item, Main.mouseItem)) {
                slotId = PlayerItemSlotID.InventoryMouseItem;
            }
            else {
                for (int i = 0; i < player.inventory.Length; i++) {
                    if (ReferenceEquals(player.inventory[i], item)) {
                        slotId = PlayerItemSlotID.Inventory0 + i;
                        break;
                    }
                }
            }
            if (slotId >= 0) {
                NetMessage.SendData(MessageID.SyncEquipment, -1, -1, null, player.whoAmI, slotId, item.prefix);
            }
        }

        private static WraithVesselHandle Resolve(List<Func<Player, WraithVesselHandle>> resolvers, Player player) {
            if (player == null || !player.active) {
                return default;
            }
            foreach (var resolver in resolvers) {
                WraithVesselHandle handle = resolver(player);
                if (handle.IsValid) {
                    return handle;
                }
            }
            return default;
        }
    }
}

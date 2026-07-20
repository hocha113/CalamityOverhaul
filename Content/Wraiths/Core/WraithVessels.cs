using System;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.Wraiths.Core
{
    /// <summary>解析出的载体：物品与其进度容器；Store 为 null 即无效</summary>
    public readonly struct WraithVesselHandle(Item item, WraithProgressStore store)
    {
        public readonly Item Item = item;
        public readonly WraithProgressStore Store = store;
        public bool IsValid => Store != null && Item != null && !Item.IsAir;
    }

    /// <summary>
    /// 载体解析缝：厉鬼框架不直接引用鬼切类型，载体方（OnikiriLegend 的 <c>OniWraithSource</c>）
    /// 在 SetupData 注册 resolver。"持鬼切门控"= <see cref="ResolveHeld"/> 只认手中之物；
    /// 反噬判定用 <see cref="ResolveCarried"/>：刀在身上，鬼就在身边，收进背包躲不掉躁动
    /// </summary>
    public static class WraithVessels
    {
        /// <summary>手持解析器表（player, 手中物品）→ handle</summary>
        private static readonly List<Func<Player, WraithVesselHandle>> heldResolvers = [];
        /// <summary>随身解析器表（含背包扫描）</summary>
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

        /// <summary>手中载体，无效 handle 表示未持刀</summary>
        public static WraithVesselHandle ResolveHeld(Player player) => Resolve(heldResolvers, player);

        /// <summary>随身载体（手中优先，背包兜底）</summary>
        public static WraithVesselHandle ResolveCarried(Player player) => Resolve(carriedResolvers, player);

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

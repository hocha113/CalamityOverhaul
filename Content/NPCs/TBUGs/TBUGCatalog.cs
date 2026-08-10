using CalamityOverhaul.Content.HackTimes.Chips;
using CalamityOverhaul.Content.RAMSystems;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.TBUGs
{
    /// <summary>商店条目：物品与基础价（铜币计）</summary>
    internal readonly record struct TBUGCatalogEntry(int ItemType, long Price);

    /// <summary>
    /// 黑客商店货表；显式登记而不是扫描基类，增删货物只改 <see cref="EnsureBuilt"/> 那张表
    /// </summary>
    internal static class TBUGCatalog
    {
        private static List<TBUGCatalogEntry> entries;
        private static Dictionary<int, TBUGCatalogEntry> byType;

        internal static IReadOnlyList<TBUGCatalogEntry> Entries {
            get {
                EnsureBuilt();
                return entries;
            }
        }

        private static void EnsureBuilt() {
            if (entries != null) {
                return;
            }
            entries = [];
            byType = [];
            //按目标族群分档陈列，顺序即货架顺序。
            //定价大体跟着协议的 RAM 消耗走，纯便利的便宜、能改战局的贵

            //生体
            Register(ModContent.ItemType<ArmorParseChip>(), Item.buyPrice(gold: 14));
            Register(ModContent.ItemType<MeltdownChip>(), Item.buyPrice(gold: 15));
            Register(ModContent.ItemType<ExorciseChip>(), Item.buyPrice(gold: 18));
            Register(ModContent.ItemType<DataLeechChip>(), Item.buyPrice(gold: 18));
            Register(ModContent.ItemType<SwarmLinkChip>(), Item.buyPrice(gold: 24));

            //弹幕
            Register(ModContent.ItemType<ProjectileFreezeChip>(), Item.buyPrice(gold: 8));
            Register(ModContent.ItemType<ProjectileHijackChip>(), Item.buyPrice(gold: 12));
            Register(ModContent.ItemType<BallisticOverclockChip>(), Item.buyPrice(gold: 12));
            Register(ModContent.ItemType<DataPurgeChip>(), Item.buyPrice(gold: 14));

            //液体
            Register(ModContent.ItemType<LiquidPurgeChip>(), Item.buyPrice(gold: 9));
            Register(ModContent.ItemType<CryostasisChip>(), Item.buyPrice(gold: 11));
            Register(ModContent.ItemType<ElectrolysisChip>(), Item.buyPrice(gold: 16));

            //掉落物
            Register(ModContent.ItemType<ItemRecallChip>(), Item.buyPrice(gold: 6));
            Register(ModContent.ItemType<ReappraiseChip>(), Item.buyPrice(gold: 22));

            //电路
            Register(ModContent.ItemType<MachineOverclockChip>(), Item.buyPrice(gold: 12));
            Register(ModContent.ItemType<TurretHijackChip>(), Item.buyPrice(gold: 20));
            Register(ModContent.ItemType<GridBlackoutChip>(), Item.buyPrice(gold: 30));

            //RAM 升级
            Register(ModContent.ItemType<RamCapacityUpgradeChip>(), Item.buyPrice(gold: 8));
            Register(ModContent.ItemType<RamRecoveryUpgradeChip>(), Item.buyPrice(gold: 6));
        }

        private static void Register(int itemType, long price) {
            if (itemType <= ItemID.None || price <= 0L || byType.ContainsKey(itemType)) {
                return;
            }
            TBUGCatalogEntry entry = new(itemType, price);
            entries.Add(entry);
            byType[itemType] = entry;
        }

        internal static bool TryGetEntry(int itemType, out TBUGCatalogEntry entry) {
            EnsureBuilt();
            return byType.TryGetValue(itemType, out entry);
        }

        /// <summary>幸福度系数换算；系数越界按 0.5~2 保险夹取，至少 1 铜</summary>
        internal static long ApplyMoodAdjustment(long basePrice, double adjustment) {
            double clamped = Math.Clamp(adjustment, 0.5, 2.0);
            return Math.Max(1L, (long)Math.Round(basePrice * clamped));
        }

        /// <summary>展示价：本机缓存的幸福度估算，仅供 UI</summary>
        internal static long GetDisplayPrice(int itemType)
            => TryGetEntry(itemType, out TBUGCatalogEntry entry)
                ? ApplyMoodAdjustment(entry.Price, TBUGMood.PriceAdjustment)
                : 0L;

        /// <summary>权威价：即时取幸福度，服务端/单机定价用</summary>
        internal static long GetAuthorityPrice(int itemType, Player player, NPC tbug) {
            if (!TryGetEntry(itemType, out TBUGCatalogEntry entry)
                || player?.active != true || tbug?.active != true) {
                return 0L;
            }
            double adjustment = Main.ShopHelper
                .GetShoppingSettings(player, tbug).PriceAdjustment;
            return ApplyMoodAdjustment(entry.Price, adjustment);
        }
    }
}

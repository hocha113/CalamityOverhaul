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
            Register(ModContent.ItemType<LiveCellTapChip>(), Item.buyPrice(gold: 9));
            Register(ModContent.ItemType<CompensationProtocolChip>(), Item.buyPrice(gold: 13));
            Register(ModContent.ItemType<ArmorParseChip>(), Item.buyPrice(gold: 14));
            Register(ModContent.ItemType<PhaseDesyncChip>(), Item.buyPrice(gold: 14));
            Register(ModContent.ItemType<MeltdownChip>(), Item.buyPrice(gold: 15));
            Register(ModContent.ItemType<ShellRequisitionChip>(), Item.buyPrice(gold: 16));
            Register(ModContent.ItemType<ExorciseChip>(), Item.buyPrice(gold: 18));
            Register(ModContent.ItemType<DataLeechChip>(), Item.buyPrice(gold: 18));
            Register(ModContent.ItemType<FirmwareRollbackChip>(), Item.buyPrice(gold: 20));
            Register(ModContent.ItemType<PayloadRewriteChip>(), Item.buyPrice(gold: 22));
            Register(ModContent.ItemType<SwarmLinkChip>(), Item.buyPrice(gold: 24));

            //弹幕
            Register(ModContent.ItemType<ProjectileFreezeChip>(), Item.buyPrice(gold: 8));
            Register(ModContent.ItemType<DelayFuseChip>(), Item.buyPrice(gold: 10));
            Register(ModContent.ItemType<ProjectileHijackChip>(), Item.buyPrice(gold: 12));
            Register(ModContent.ItemType<BallisticOverclockChip>(), Item.buyPrice(gold: 12));
            Register(ModContent.ItemType<ProjectileTitheChip>(), Item.buyPrice(gold: 13));
            Register(ModContent.ItemType<DataPurgeChip>(), Item.buyPrice(gold: 14));
            Register(ModContent.ItemType<ProjectileSampleChip>(), Item.buyPrice(gold: 15));

            //液体
            Register(ModContent.ItemType<LiquidPurgeChip>(), Item.buyPrice(gold: 9));
            Register(ModContent.ItemType<PressureSurgeChip>(), Item.buyPrice(gold: 10));
            Register(ModContent.ItemType<CryostasisChip>(), Item.buyPrice(gold: 11));
            Register(ModContent.ItemType<ElectrolysisChip>(), Item.buyPrice(gold: 16));
            Register(ModContent.ItemType<MirrorSurfaceChip>(), Item.buyPrice(gold: 18));

            //物块
            Register(ModContent.ItemType<VeinResonanceChip>(), Item.buyPrice(gold: 11));
            Register(ModContent.ItemType<StressInvertChip>(), Item.buyPrice(gold: 13));
            Register(ModContent.ItemType<TileConscriptChip>(), Item.buyPrice(gold: 14));

            //掉落物
            Register(ModContent.ItemType<ItemRecallChip>(), Item.buyPrice(gold: 6));
            Register(ModContent.ItemType<ItemSalvageChip>(), Item.buyPrice(gold: 10));
            Register(ModContent.ItemType<EntityMasqueradeChip>(), Item.buyPrice(gold: 12));
            Register(ModContent.ItemType<ReappraiseChip>(), Item.buyPrice(gold: 22));
            Register(ModContent.ItemType<DataBrandChip>(), Item.buyPrice(gold: 26));

            //容器
            Register(ModContent.ItemType<IndexPrereadChip>(), Item.buyPrice(gold: 7));
            Register(ModContent.ItemType<LockBurnChip>(), Item.buyPrice(gold: 16));

            //世界
            Register(ModContent.ItemType<DielSkipChip>(), Item.buyPrice(gold: 20));
            Register(ModContent.ItemType<GravityInvertChip>(), Item.buyPrice(gold: 22));
            Register(ModContent.ItemType<StormInjectChip>(), Item.buyPrice(gold: 24));

            //部件
            Register(ModContent.ItemType<SegmentDelinkChip>(), Item.buyPrice(gold: 18));
            Register(ModContent.ItemType<LimbSeizureChip>(), Item.buyPrice(gold: 20));
            Register(ModContent.ItemType<CommandLinkCutChip>(), Item.buyPrice(gold: 24));

            //自体
            Register(ModContent.ItemType<PowerTransmuteChip>(), Item.buyPrice(gold: 16));
            Register(ModContent.ItemType<NeuralOverclockChip>(), Item.buyPrice(gold: 18));
            Register(ModContent.ItemType<WraithForceDriveChip>(), Item.buyPrice(gold: 20));

            //电路（F14 落地后炮塔劫持/电网瘫痪回架；电网瘫痪是全店天花板价）
            Register(ModContent.ItemType<MachineOverclockChip>(), Item.buyPrice(gold: 12));
            Register(ModContent.ItemType<TurretHijackChip>(), Item.buyPrice(gold: 13));
            Register(ModContent.ItemType<MunitionSwapChip>(), Item.buyPrice(gold: 14));
            Register(ModContent.ItemType<BeaconForgeChip>(), Item.buyPrice(gold: 16));
            Register(ModContent.ItemType<TurretMeshChip>(), Item.buyPrice(gold: 20));
            Register(ModContent.ItemType<PrivilegeEscalateChip>(), Item.buyPrice(gold: 26));
            Register(ModContent.ItemType<GridBlackoutChip>(), Item.buyPrice(gold: 30));

            //PvP 骇入（2026-08 芯片档：攻击方结算簇 + 防守方本机结算簇）
            Register(ModContent.ItemType<ChannelScrambleChip>(), Item.buyPrice(gold: 14));
            Register(ModContent.ItemType<MapBlackoutChip>(), Item.buyPrice(gold: 16));
            Register(ModContent.ItemType<StealthStripChip>(), Item.buyPrice(gold: 18));
            Register(ModContent.ItemType<MemoryScorchChip>(), Item.buyPrice(gold: 20));
            Register(ModContent.ItemType<BuffSiphonChip>(), Item.buyPrice(gold: 22));
            Register(ModContent.ItemType<CombatSiphonChip>(), Item.buyPrice(gold: 22));
            Register(ModContent.ItemType<CooldownInjectChip>(), Item.buyPrice(gold: 22));
            Register(ModContent.ItemType<BallisticTurncoatChip>(), Item.buyPrice(gold: 26));
            Register(ModContent.ItemType<CyberwareOfflineChip>(), Item.buyPrice(gold: 26));
            Register(ModContent.ItemType<MeltdownBrandChip>(), Item.buyPrice(gold: 28));

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

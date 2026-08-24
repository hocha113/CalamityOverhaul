using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.NPCs.TBUGs
{
    /// <summary>
    /// 黑客商店的世界级库存表；服务端/单机权威，客户端只持镜像。
    /// 每个黎明全量补货并推进纪元；每日特惠名单由纪元确定性推导，各端不需单独同步
    /// </summary>
    internal static class TBUGStock
    {
        /// <summary>特惠折扣系数</summary>
        internal const double SpecialFactor = 0.75;
        /// <summary>每次补货抽出的特惠件数</summary>
        private const int SpecialCount = 2;

        //物品类型 → 剩余件数；不在表里视为 0
        private static readonly Dictionary<int, int> stock = [];
        private static readonly HashSet<int> specials = [];

        /// <summary>补货纪元；0 表示这个世界从未铺过货</summary>
        internal static int RestockEpoch { get; private set; }

        internal static int GetStock(int itemType)
            => stock.TryGetValue(itemType, out int value) ? value : 0;

        internal static bool IsSpecial(int itemType) => specials.Contains(itemType);

        /// <summary>权威端成交扣减；余量不足返回 false</summary>
        internal static bool Consume(int itemType) {
            int current = GetStock(itemType);
            if (current <= 0) {
                return false;
            }
            stock[itemType] = current - 1;
            return true;
        }

        /// <summary>权威端退货（客户端结算失败回滚），封顶到补货上限</summary>
        internal static void Refund(int itemType) {
            if (!TBUGCatalog.TryGetEntry(itemType, out TBUGCatalogEntry entry)) {
                return;
            }
            stock[itemType] = Math.Min(entry.MaxStock, GetStock(itemType) + 1);
        }

        /// <summary>客户端镜像：应用权威端广播的单件余量</summary>
        internal static void SetStock(int itemType, int value) {
            if (!TBUGCatalog.TryGetEntry(itemType, out TBUGCatalogEntry entry)) {
                return;
            }
            stock[itemType] = Math.Clamp(value, 0, entry.MaxStock);
        }

        /// <summary>全量补货并推进纪元；权威端黎明沿与新世界首次铺货共用</summary>
        internal static void RestockAll(int epoch) {
            RestockEpoch = epoch;
            stock.Clear();
            foreach (TBUGCatalogEntry entry in TBUGCatalog.Entries) {
                stock[entry.ItemType] = entry.MaxStock;
            }
            RollSpecials();
        }

        /// <summary>
        /// 按纪元抽特惠名单；确定性 LCG，各端只要纪元一致名单必一致。
        /// 纪元 0（未铺货）不给特惠
        /// </summary>
        private static void RollSpecials() {
            specials.Clear();
            IReadOnlyList<TBUGCatalogEntry> entries = TBUGCatalog.Entries;
            if (entries.Count == 0 || RestockEpoch <= 0) {
                return;
            }
            uint state = (uint)RestockEpoch * 2654435761u | 1u;
            int guard = 0;
            while (specials.Count < SpecialCount && guard++ < 64) {
                state = state * 1664525u + 1013904223u;
                specials.Add(entries[(int)(state % (uint)entries.Count)].ItemType);
            }
        }

        /// <summary>当前库存快照，供存档与网络同步遍历</summary>
        internal static IReadOnlyDictionary<int, int> Export() => stock;

        /// <summary>客户端应用权威端快照（入世同步与补货广播共用）</summary>
        internal static void ApplyNet(int epoch, List<(int itemType, int count)> entries) {
            RestockEpoch = epoch;
            stock.Clear();
            foreach ((int itemType, int count) in entries) {
                SetStock(itemType, count);
            }
            RollSpecials();
        }

        /// <summary>
        /// 存档回读：先按上限铺满再覆写存档值，存档里没有的条目（更新后新增的货）
        /// 直接给满货，别让新内容等到下一个黎明
        /// </summary>
        internal static void ImportSave(int epoch, IList<string> names, IList<int> counts) {
            Clear();
            if (epoch <= 0) {
                return;
            }
            RestockEpoch = epoch;
            foreach (TBUGCatalogEntry entry in TBUGCatalog.Entries) {
                stock[entry.ItemType] = entry.MaxStock;
            }
            if (names != null && counts != null) {
                int n = Math.Min(names.Count, counts.Count);
                for (int i = 0; i < n; i++) {
                    //物品被后续版本移除时 TryFind 落空，跳过即可
                    if (ModContent.TryFind(names[i], out ModItem item)) {
                        SetStock(item.Type, counts[i]);
                    }
                }
            }
            RollSpecials();
        }

        internal static void Clear() {
            stock.Clear();
            specials.Clear();
            RestockEpoch = 0;
        }
    }

    /// <summary>
    /// 库存的世界生命周期：存档持久化、入世快照、黎明沿全量补货。
    /// 骨架照 <see cref="TBUGWorldState"/>，权威端判定与广播职责都收在这里
    /// </summary>
    internal class TBUGStockSystem : ModSystem
    {
        private static bool wasDayTime;

        public override void ClearWorld() => TBUGStock.Clear();

        public override void OnWorldLoad() {
            wasDayTime = Main.dayTime;
            if (VaultUtils.isClient) {
                //客户端等 NetReceive 的入世快照
                return;
            }
            if (TBUGStock.RestockEpoch <= 0) {
                //新世界（或旧档迁移）首次铺货
                TBUGStock.RestockAll(1);
            }
        }

        public override void PostUpdateTime() {
            if (VaultUtils.isClient) {
                return;
            }
            bool day = Main.dayTime;
            if (day && !wasDayTime) {
                //黎明沿：全量补货、换一批特惠，多人下广播全表
                TBUGStock.RestockAll(TBUGStock.RestockEpoch + 1);
                TBUGShopNet.BroadcastStockSync();
            }
            wasDayTime = day;
        }

        public override void SaveWorldData(TagCompound tag) {
            if (TBUGStock.RestockEpoch <= 0) {
                return;
            }
            tag["restockEpoch"] = TBUGStock.RestockEpoch;
            List<string> names = [];
            List<int> counts = [];
            foreach ((int itemType, int count) in TBUGStock.Export()) {
                if (ItemLoader.GetItem(itemType) is not ModItem modItem) {
                    continue;
                }
                names.Add(modItem.FullName);
                counts.Add(count);
            }
            tag["stockNames"] = names;
            tag["stockCounts"] = counts;
        }

        public override void LoadWorldData(TagCompound tag) {
            int epoch = tag.TryGet("restockEpoch", out int savedEpoch) ? savedEpoch : 0;
            tag.TryGet("stockNames", out List<string> names);
            tag.TryGet("stockCounts", out List<int> counts);
            TBUGStock.ImportSave(epoch, names, counts);
        }

        public override void NetSend(BinaryWriter writer) {
            writer.Write(TBUGStock.RestockEpoch);
            IReadOnlyDictionary<int, int> export = TBUGStock.Export();
            writer.Write((short)export.Count);
            foreach ((int itemType, int count) in export) {
                writer.Write(itemType);
                writer.Write((short)count);
            }
        }

        public override void NetReceive(BinaryReader reader) {
            int epoch = reader.ReadInt32();
            int n = reader.ReadInt16();
            List<(int, int)> entries = new(Math.Max(0, n));
            for (int i = 0; i < n; i++) {
                entries.Add((reader.ReadInt32(), reader.ReadInt16()));
            }
            TBUGStock.ApplyNet(epoch, entries);
        }
    }
}

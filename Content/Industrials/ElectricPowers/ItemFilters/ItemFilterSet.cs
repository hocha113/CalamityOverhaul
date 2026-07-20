using System;
using System.Collections.Generic;
using System.IO;
using Terraria.ID;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.ItemFilters
{
    /// <summary>
    /// 过滤器的匹配模式
    /// </summary>
    internal enum ItemFilterMode : byte
    {
        /// <summary>白名单：仅名单内的物品通过</summary>
        Whitelist = 0,
        /// <summary>黑名单：名单内的物品被排除</summary>
        Blacklist = 1
    }

    /// <summary>
    /// 物品过滤名单的纯数据模型：有序去重的物品ID集合 + 匹配模式<br/>
    /// 序列化、网络编解码、匹配语义全部收敛于此，任何宿主(手持卡、收集器、管道等)都持有一个实例<br/>
    /// 语义约定：<b>空名单 = 不限制(全部通过)</b>，无论黑白名单模式<br/>
    /// 线程安全：<see cref="Matches"/>/<see cref="Contains"/>可在并行Update中调用(查询集采用整体替换而非原地修改)，
    /// 所有修改操作仅允许主线程调用
    /// </summary>
    internal sealed class ItemFilterSet
    {
        /// <summary>名单容量上限，防御网络与存档层面的异常数据</summary>
        public const int MaxEntries = 500;

        //展示顺序(录入顺序)，仅主线程读写
        private readonly List<int> ordered = [];
        //O(1)查询集。修改时整体重建后替换引用，使并行读取端永远看到完整的旧集或新集
        private HashSet<int> lookup = [];

        /// <summary>匹配模式</summary>
        public ItemFilterMode Mode { get; private set; } = ItemFilterMode.Whitelist;

        /// <summary>
        /// 本机修改版本号，每次内容变化自增<br/>
        /// 仅用于本机缓存失效判断，<b>禁止用于跨网络的数据仲裁</b>(不同机器各自自增，不可比较)
        /// </summary>
        public int Revision { get; private set; }

        /// <summary>按录入顺序枚举名单(仅主线程使用)</summary>
        public IReadOnlyList<int> OrderedItems => ordered;

        public int Count => ordered.Count;

        public bool IsEmpty => ordered.Count == 0;

        /// <summary>名单是否包含该物品ID(可并行调用)</summary>
        public bool Contains(int itemType) => lookup.Contains(itemType);

        /// <summary>
        /// 该物品是否被过滤器放行(可并行调用)：空名单一律放行；
        /// 白名单要求在名单内，黑名单要求不在名单内
        /// </summary>
        public bool Matches(int itemType) {
            HashSet<int> snapshot = lookup;
            if (snapshot.Count == 0) {
                return true;
            }
            bool inSet = snapshot.Contains(itemType);
            return Mode == ItemFilterMode.Whitelist ? inSet : !inSet;
        }

        /// <summary>录入一个物品ID，无效、重复或超出容量时返回<see langword="false"/></summary>
        public bool Add(int itemType) {
            if (itemType <= ItemID.None || ordered.Count >= MaxEntries || lookup.Contains(itemType)) {
                return false;
            }
            ordered.Add(itemType);
            RebuildLookup();
            Revision++;
            return true;
        }

        /// <summary>移除一个物品ID</summary>
        public bool Remove(int itemType) {
            if (!ordered.Remove(itemType)) {
                return false;
            }
            RebuildLookup();
            Revision++;
            return true;
        }

        public void Clear() {
            if (ordered.Count == 0) {
                return;
            }
            ordered.Clear();
            lookup = [];
            Revision++;
        }

        public void SetMode(ItemFilterMode mode) {
            if (Mode == mode) {
                return;
            }
            Mode = mode;
            Revision++;
        }

        public void ToggleMode()
            => SetMode(Mode == ItemFilterMode.Whitelist ? ItemFilterMode.Blacklist : ItemFilterMode.Whitelist);

        /// <summary>整体复制另一个名单(内容与模式)</summary>
        public void CopyFrom(ItemFilterSet other) {
            if (other == null) {
                return;
            }
            CopyFrom(other.ordered, other.Mode);
        }

        /// <summary>用给定内容与模式整体重置名单，自动去重、剔除无效ID并截断到容量上限</summary>
        public void CopyFrom(IEnumerable<int> itemTypes, ItemFilterMode mode) {
            ordered.Clear();
            HashSet<int> fresh = [];
            if (itemTypes != null) {
                foreach (int type in itemTypes) {
                    if (type <= ItemID.None || fresh.Contains(type)) {
                        continue;
                    }
                    ordered.Add(type);
                    fresh.Add(type);
                    if (ordered.Count >= MaxEntries) {
                        break;
                    }
                }
            }
            lookup = fresh;
            Mode = mode;
            Revision++;
        }

        private void RebuildLookup() {
            HashSet<int> fresh = new(ordered.Count);
            foreach (int type in ordered) {
                fresh.Add(type);
            }
            lookup = fresh;
        }

        #region 序列化

        public void Write(BinaryWriter writer) {
            writer.Write((byte)Mode);
            int count = Math.Min(ordered.Count, MaxEntries);
            writer.Write((ushort)count);
            for (int i = 0; i < count; i++) {
                writer.Write(ordered[i]);
            }
        }

        public void Read(BinaryReader reader) {
            byte modeByte = reader.ReadByte();
            ItemFilterMode mode = modeByte <= (byte)ItemFilterMode.Blacklist
                ? (ItemFilterMode)modeByte
                : ItemFilterMode.Whitelist;

            int count = Math.Min((int)reader.ReadUInt16(), MaxEntries);
            List<int> received = new(count);
            for (int i = 0; i < count; i++) {
                received.Add(reader.ReadInt32());
            }
            CopyFrom(received, mode);
        }

        public void Save(TagCompound tag, string key) {
            tag[key + "Mode"] = (byte)Mode;
            tag[key + "Items"] = ordered.ToArray();
        }

        /// <summary>读取新格式存档数据，存在对应键时返回<see langword="true"/></summary>
        public bool TryLoad(TagCompound tag, string key) {
            if (tag == null || !tag.TryGet(key + "Items", out int[] items)) {
                return false;
            }
            ItemFilterMode mode = ItemFilterMode.Whitelist;
            if (tag.TryGet(key + "Mode", out byte modeByte) && modeByte <= (byte)ItemFilterMode.Blacklist) {
                mode = (ItemFilterMode)modeByte;
            }
            CopyFrom(items, mode);
            return true;
        }

        #endregion
    }
}

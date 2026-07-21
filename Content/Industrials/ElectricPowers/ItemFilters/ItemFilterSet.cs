using System;
using System.Collections.Generic;
using System.IO;
using Terraria.ID;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.ItemFilters
{
    /// <summary>过滤匹配模式</summary>
    internal enum ItemFilterMode : byte
    {
        /// <summary>白名单，仅名单内通过</summary>
        Whitelist = 0,
        /// <summary>黑名单，名单内排除</summary>
        Blacklist = 1
    }

    /// <summary>
    /// 有序去重物品ID + 匹配模式；空名单=不限制<br/>
    /// <see cref="Matches"/>/<see cref="Contains"/>可并行读(lookup整体替换)；修改仅主线程
    /// </summary>
    internal sealed class ItemFilterSet
    {
        /// <summary>容量上限</summary>
        public const int MaxEntries = 500;

        //录入顺序，仅主线程读写
        private readonly List<int> ordered = [];
        //O(1)查询；修改时整体重建替换引用，并行读端永远看到完整旧/新集
        private HashSet<int> lookup = [];

        public ItemFilterMode Mode { get; private set; } = ItemFilterMode.Whitelist;

        /// <summary>本机修改版本，禁跨网比较</summary>
        public int Revision { get; private set; }

        /// <summary>按录入顺序(仅主线程)</summary>
        public IReadOnlyList<int> OrderedItems => ordered;

        public int Count => ordered.Count;

        public bool IsEmpty => ordered.Count == 0;

        /// <summary>可并行</summary>
        public bool Contains(int itemType) => lookup.Contains(itemType);

        /// <summary>空名单放行；白/黑按是否在名单(可并行)</summary>
        public bool Matches(int itemType) {
            HashSet<int> snapshot = lookup;
            if (snapshot.Count == 0) {
                return true;
            }
            bool inSet = snapshot.Contains(itemType);
            return Mode == ItemFilterMode.Whitelist ? inSet : !inSet;
        }

        public bool Add(int itemType) {
            if (itemType <= ItemID.None || ordered.Count >= MaxEntries || lookup.Contains(itemType)) {
                return false;
            }
            ordered.Add(itemType);
            RebuildLookup();
            Revision++;
            return true;
        }

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

        public void CopyFrom(ItemFilterSet other) {
            if (other == null) {
                return;
            }
            CopyFrom(other.ordered, other.Mode);
        }

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

        /// <summary>读新格式存档，有键返回true</summary>
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

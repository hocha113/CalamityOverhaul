using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.Inscriptions.Deeds
{
    /// <summary>
    /// 刀縁进度（跟玩家存档，与 <see cref="OnikiriPlayer.OwnedMeiKeys"/> 并列）。<br/>
    /// 已所持的铭其縁视作已结，不再累计；去重记号按縁分袋
    /// </summary>
    internal sealed class OniMeiDeedProgress
    {
        private const string CountTag = "OniMeiDeedKeys";
        private const string ValueTag = "OniMeiDeedValues";
        private const string MarkKeyTag = "OniMeiDeedMarkKeys";
        private const string MarkCountTag = "OniMeiDeedMarkCounts";
        private const string MarkValueTag = "OniMeiDeedMarkValues";
        /// <summary>单縁记号袋上限，防脏档灌爆</summary>
        private const int MaxMarksPerDeed = 256;

        private readonly Dictionary<string, int> counts = [];
        private readonly Dictionary<string, HashSet<int>> marks = [];

        /// <summary>该縁已累计多少</summary>
        internal int Get(string deedKey)
            => deedKey != null && counts.TryGetValue(deedKey, out int value) ? value : 0;

        /// <summary>该縁是否已结：铭已所持即算结，否则看累计是否到量</summary>
        internal bool IsSettled(Player player, OniMeiDeed deed) {
            if (deed == null) {
                return true;
            }
            return OniMeiOwned.Owns(player, deed.MeiKey) || Get(deed.Key) >= Math.Max(1, deed.NeedCount);
        }

        /// <summary>
        /// 推进一笔。到量即解锁并落拓本；返回是否在本次推进中结縁
        /// </summary>
        internal bool Advance(Player player, OniMeiDeed deed, int amount, int mark) {
            if (player == null || deed == null || amount <= 0 || IsSettled(player, deed)) {
                return false;
            }
            if (mark != 0 && !AddMark(deed.Key, mark)) {
                return false;
            }
            int need = Math.Max(1, deed.NeedCount);
            int value = Math.Min(need, Get(deed.Key) + amount);
            counts[deed.Key] = value;
            if (value < need) {
                OniMeiDeedRite.PlayTick(player, deed, value, need);
                return false;
            }
            marks.Remove(deed.Key);
            OniMeiDeedRite.PlaySettle(player, deed);
            return true;
        }

        private bool AddMark(string deedKey, int mark) {
            if (!marks.TryGetValue(deedKey, out HashSet<int> bag)) {
                bag = [];
                marks[deedKey] = bag;
            }
            return bag.Count < MaxMarksPerDeed && bag.Add(mark);
        }

        internal void Clear() {
            counts.Clear();
            marks.Clear();
        }

        //====存档====

        internal void Save(TagCompound tag) {
            List<string> keys = [];
            List<int> values = [];
            foreach ((string key, int value) in counts) {
                if (value > 0 && OniMeiDeedRegistry.TryGet(key, out _)) {
                    keys.Add(key);
                    values.Add(value);
                }
            }
            tag[CountTag] = keys;
            tag[ValueTag] = values;

            List<string> markKeys = [];
            List<int> markCounts = [];
            List<int> markValues = [];
            foreach ((string key, HashSet<int> bag) in marks) {
                if (bag.Count <= 0 || !OniMeiDeedRegistry.TryGet(key, out _)) {
                    continue;
                }
                markKeys.Add(key);
                markCounts.Add(bag.Count);
                markValues.AddRange(bag);
            }
            tag[MarkKeyTag] = markKeys;
            tag[MarkCountTag] = markCounts;
            tag[MarkValueTag] = markValues;
        }

        internal void Load(TagCompound tag) {
            Clear();
            if (tag.TryGet(CountTag, out List<string> keys) && keys != null
                && tag.TryGet(ValueTag, out List<int> values) && values != null) {
                int n = Math.Min(keys.Count, values.Count);
                for (int i = 0; i < n; i++) {
                    if (!string.IsNullOrEmpty(keys[i]) && values[i] > 0) {
                        counts[keys[i]] = values[i];
                    }
                }
            }
            if (!tag.TryGet(MarkKeyTag, out List<string> markKeys) || markKeys == null
                || !tag.TryGet(MarkCountTag, out List<int> markCounts) || markCounts == null
                || !tag.TryGet(MarkValueTag, out List<int> markValues) || markValues == null) {
                return;
            }
            int cursor = 0;
            int bags = Math.Min(markKeys.Count, markCounts.Count);
            for (int i = 0; i < bags; i++) {
                int size = Math.Clamp(markCounts[i], 0, MaxMarksPerDeed);
                if (cursor + size > markValues.Count) {
                    break;
                }
                if (!string.IsNullOrEmpty(markKeys[i]) && size > 0) {
                    HashSet<int> bag = [];
                    for (int m = 0; m < size; m++) {
                        bag.Add(markValues[cursor + m]);
                    }
                    marks[markKeys[i]] = bag;
                }
                cursor += size;
            }
        }

        //====联机快照（客户端→服务器，与所持铭快照同口径）====

        internal void Write(BinaryWriter writer) {
            List<(ushort Id, int Value)> entries = [];
            foreach ((string key, int value) in counts) {
                if (value > 0 && OniMeiDeedRegistry.TryGetNetworkId(key, out ushort id)) {
                    entries.Add((id, value));
                }
            }
            entries.Sort((a, b) => a.Id.CompareTo(b.Id));
            writer.Write((ushort)entries.Count);
            foreach ((ushort id, int value) in entries) {
                writer.Write(id);
                writer.Write((ushort)Math.Clamp(value, 0, ushort.MaxValue));
            }
        }

        internal void Read(BinaryReader reader) {
            int count = ReadEntryCount(reader);
            Clear();
            for (int i = 0; i < count; i++) {
                ushort id = reader.ReadUInt16();
                int value = reader.ReadUInt16();
                if (value > 0 && OniMeiDeedRegistry.TryGetByNetworkId(id, out OniMeiDeed deed)) {
                    counts[deed.Key] = Math.Min(value, Math.Max(1, deed.NeedCount));
                }
            }
        }

        /// <summary>无处安放时把本段读干净，否则同包后续分支会错位</summary>
        internal static void Skip(BinaryReader reader) {
            int count = ReadEntryCount(reader);
            for (int i = 0; i < count; i++) {
                reader.ReadUInt16();
                reader.ReadUInt16();
            }
        }

        /// <summary>条目数一律按注册表容量封顶，脏包不至于读穿</summary>
        private static int ReadEntryCount(BinaryReader reader)
            => Math.Min(reader.ReadUInt16(), OniMeiDeedRegistry.All.Count);
    }
}

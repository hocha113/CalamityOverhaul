using CalamityOverhaul.Common;
using System.Collections.Generic;
using System.IO;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaTalismans
{
    /// <summary>
    /// 每伞符位表：符位下标→符 Key，空串即空位；三位同质，同 Key 全表唯一。<br/>
    /// 宿主无关，存档与联机入口统一消毒；表现层数据缝，效果层后补不改结构
    /// </summary>
    public sealed class KikasaTalismanStore
    {
        /// <summary>schema 版本，结构变更递增</summary>
        public const int SchemaVersion = 1;

        /// <summary>祈雨绳符位数</summary>
        public const int SlotCount = 3;

        private const int MaxKeyBytes = 256;

        private readonly string[] slots = new string[SlotCount];

        /// <summary>变更版本，展示层脏检查</summary>
        public int Version { get; private set; }

        public void BumpVersion() => Version++;

        /// <summary>取符位上的符 Key，空位 null</summary>
        public string Get(int slot)
            => slot >= 0 && slot < SlotCount && !string.IsNullOrEmpty(slots[slot]) ? slots[slot] : null;

        /// <summary>该 Key 是否已挂在任一符位</summary>
        public bool Contains(string key) {
            if (string.IsNullOrEmpty(key)) {
                return false;
            }
            for (int i = 0; i < SlotCount; i++) {
                if (slots[i] == key) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>已挂符数</summary>
        public int HungCount {
            get {
                int count = 0;
                for (int i = 0; i < SlotCount; i++) {
                    if (!string.IsNullOrEmpty(slots[i])) {
                        count++;
                    }
                }
                return count;
            }
        }

        /// <summary>挂符/换符；Key 未注册、同位同符或已挂在他位则拒绝</summary>
        public bool Hang(int slot, string key) {
            if (slot < 0 || slot >= SlotCount || string.IsNullOrEmpty(key)
                || !KikasaTalismanRegistry.TryGet(key, out _)) {
                return false;
            }
            if (Get(slot) == key || Contains(key)) {
                return false;
            }
            slots[slot] = key;
            BumpVersion();
            return true;
        }

        /// <summary>摘符，空位无事</summary>
        public bool TakeDown(int slot) {
            if (slot < 0 || slot >= SlotCount || Get(slot) == null) {
                return false;
            }
            slots[slot] = null;
            BumpVersion();
            return true;
        }

        /// <summary>深拷贝，物品克隆链用</summary>
        public void CopyFrom(KikasaTalismanStore source) {
            ReplaceWithSanitized(ReadStoreEntries(source));
        }

        public void Clear() {
            for (int i = 0; i < SlotCount; i++) {
                slots[i] = null;
            }
            BumpVersion();
        }

        /// <summary>存入宿主 tag，键带 KikasaFu 前缀</summary>
        public void SaveData(TagCompound tag) {
            List<TagCompound> list = [];
            Dictionary<int, string> sanitized = Sanitize(ReadStoreEntries(this));
            for (int slot = 0; slot < SlotCount; slot++) {
                if (!sanitized.TryGetValue(slot, out string key)) {
                    continue;
                }
                list.Add(new TagCompound {
                    ["Slot"] = (byte)slot,
                    ["Key"] = key,
                });
            }
            if (list.Count == 0) {
                return;
            }
            tag["KikasaFu:Version"] = SchemaVersion;
            tag["KikasaFu:Slots"] = list;
        }

        public void LoadData(TagCompound tag) {
            if (!tag.TryGet("KikasaFu:Slots", out List<TagCompound> list) || list == null) {
                ReplaceWithSanitized([]);
                return;
            }
            tag.TryGet("KikasaFu:Version", out int _);
            ReplaceWithSanitized(ReadTagEntries(list));
        }

        //====联机序列化====

        public void NetSend(BinaryWriter writer) {
            Dictionary<int, string> sanitized = Sanitize(ReadStoreEntries(this));
            writer.Write((byte)sanitized.Count);
            for (int slot = 0; slot < SlotCount; slot++) {
                if (!sanitized.TryGetValue(slot, out string key)) {
                    continue;
                }
                writer.Write((byte)slot);
                CWRNetGuard.WriteString(writer, key, MaxKeyBytes);
            }
        }

        public void NetReceive(BinaryReader reader) {
            int count = reader.ReadByte();
            if (count > SlotCount) {
                throw new IOException($"KikasaFu.Slots count {count} exceeds 0..{SlotCount}");
            }
            List<(int RawSlot, string Key)> entries = new(count);
            for (int i = 0; i < count; i++) {
                byte rawSlot = reader.ReadByte();
                string key = CWRNetGuard.ReadString(reader, MaxKeyBytes, "KikasaFu.Key");
                entries.Add((rawSlot, key));
            }
            ReplaceWithSanitized(entries);
        }

        private static IEnumerable<(int RawSlot, string Key)> ReadStoreEntries(KikasaTalismanStore store) {
            if (store == null) {
                yield break;
            }
            for (int slot = 0; slot < SlotCount; slot++) {
                if (!string.IsNullOrEmpty(store.slots[slot])) {
                    yield return (slot, store.slots[slot]);
                }
            }
        }

        private static IEnumerable<(int RawSlot, string Key)> ReadTagEntries(List<TagCompound> list) {
            foreach (TagCompound entry in list) {
                if (!entry.TryGet("Key", out string key)) {
                    continue;
                }
                if (entry.TryGet("Slot", out byte byteSlot)) {
                    yield return (byteSlot, key);
                }
                else if (entry.TryGet("Slot", out int intSlot)) {
                    yield return (intSlot, key);
                }
            }
        }

        /// <summary>消毒：位越界、Key 未注册、位重复或 Key 重复一律丢弃</summary>
        private static Dictionary<int, string> Sanitize(IEnumerable<(int RawSlot, string Key)> entries) {
            Dictionary<int, string> sanitized = [];
            HashSet<string> usedKeys = [];
            foreach ((int rawSlot, string key) in entries) {
                if (rawSlot < 0 || rawSlot >= SlotCount || string.IsNullOrWhiteSpace(key)) {
                    continue;
                }
                if (sanitized.ContainsKey(rawSlot) || usedKeys.Contains(key)
                    || !KikasaTalismanRegistry.TryGet(key, out _)) {
                    continue;
                }
                sanitized.Add(rawSlot, key);
                usedKeys.Add(key);
            }
            return sanitized;
        }

        private void ReplaceWithSanitized(IEnumerable<(int RawSlot, string Key)> entries) {
            Dictionary<int, string> sanitized = Sanitize(entries);
            for (int slot = 0; slot < SlotCount; slot++) {
                slots[slot] = sanitized.TryGetValue(slot, out string key) ? key : null;
            }
            BumpVersion();
        }
    }
}

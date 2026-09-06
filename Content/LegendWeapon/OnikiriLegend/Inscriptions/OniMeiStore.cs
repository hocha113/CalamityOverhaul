using CalamityOverhaul.Common;
using System.Collections.Generic;
using System.IO;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.Inscriptions
{
    /// <summary>
    /// 每刀铭位表：铭位→铭 Key，空串即空位；宿主无关，存档与联机入口统一消毒。<br/>
    /// 表现层数据缝，效果层后补不改结构
    /// </summary>
    public sealed class OniMeiStore
    {
        /// <summary>schema 版本，结构变更递增</summary>
        public const int SchemaVersion = 2;

        private static readonly OniMeiSlotKind[] slotKinds =
            [OniMeiSlotKind.Nakago, OniMeiSlotKind.Hi, OniMeiSlotKind.Horimono];

        private const int MaxKeyBytes = 256;

        private readonly Dictionary<OniMeiSlotKind, string> slots = [];

        /// <summary>变更版本，展示层脏检查</summary>
        public int Version { get; private set; }

        public void BumpVersion() => Version++;

        /// <summary>全部铭位种类，展示层遍历用</summary>
        public static IReadOnlyList<OniMeiSlotKind> SlotKinds => slotKinds;

        /// <summary>取铭位上的铭 Key，空位 null</summary>
        public string Get(OniMeiSlotKind slot)
            => slots.TryGetValue(slot, out string key) && !string.IsNullOrEmpty(key) ? key : null;

        /// <summary>凿铭/改铭；铭位与定义铭位不符则拒绝</summary>
        public bool Engrave(OniMeiSlotKind slot, string key) {
            if (string.IsNullOrEmpty(key)
                || !OniMeiRegistry.TryGet(key, out OniMeiDefinition definition)
                || definition.SlotKind != slot) {
                return false;
            }
            if (Get(slot) == key) {
                return false;
            }
            slots[slot] = key;
            BumpVersion();
            return true;
        }

        /// <summary>除铭，空位无事</summary>
        public bool Erase(OniMeiSlotKind slot) {
            if (Get(slot) == null) {
                return false;
            }
            slots.Remove(slot);
            BumpVersion();
            return true;
        }

        /// <summary>深拷贝，物品克隆链用</summary>
        public void CopyFrom(OniMeiStore source) {
            ReplaceWithSanitized(ReadStoreEntries(source));
        }

        public void Clear() {
            slots.Clear();
            BumpVersion();
        }

        /// <summary>存入宿主 tag，键带 OniMei 前缀</summary>
        public void SaveData(TagCompound tag) {
            List<TagCompound> list = [];
            Dictionary<OniMeiSlotKind, string> sanitized = Sanitize(ReadStoreEntries(this));
            foreach (OniMeiSlotKind slot in slotKinds) {
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
            tag["OniMei:Version"] = SchemaVersion;
            tag["OniMei:Slots"] = list;
        }

        public void LoadData(TagCompound tag) {
            if (!tag.TrySafeGet("OniMei:Slots", out List<TagCompound> list, nameof(OniMeiStore)) || list == null) {
                ReplaceWithSanitized([]);
                return;
            }
            //v1 与 v2 条目形状相同，旧数据直接迁入
            ReplaceWithSanitized(ReadTagEntries(list));
        }

        //====联机序列化====

        public void NetSend(BinaryWriter writer) {
            Dictionary<OniMeiSlotKind, string> sanitized = Sanitize(ReadStoreEntries(this));
            writer.Write((byte)sanitized.Count);
            foreach (OniMeiSlotKind slot in slotKinds) {
                if (!sanitized.TryGetValue(slot, out string key)) {
                    continue;
                }
                writer.Write((byte)slot);
                CWRNetGuard.WriteString(writer, key, MaxKeyBytes);
            }
        }

        public void NetReceive(BinaryReader reader) {
            int count = reader.ReadByte();
            if (count > slotKinds.Length) {
                throw new IOException($"OniMei.Slots count {count} exceeds 0..{slotKinds.Length}");
            }
            List<(int RawSlot, string Key)> entries = new(count);
            for (int i = 0; i < count; i++) {
                byte rawSlot = reader.ReadByte();
                string key = CWRNetGuard.ReadString(reader, MaxKeyBytes, "OniMei.Key");
                entries.Add((rawSlot, key));
            }
            ReplaceWithSanitized(entries);
        }

        private static IEnumerable<(int RawSlot, string Key)> ReadStoreEntries(OniMeiStore store) {
            if (store == null) {
                yield break;
            }
            foreach (OniMeiSlotKind slot in slotKinds) {
                if (store.slots.TryGetValue(slot, out string key)) {
                    yield return ((int)slot, key);
                }
            }
        }

        //Slot 历史上有 byte 与 int 两种存法；TryGet<byte> 碰到 int 会直接抛而不是落到下一分支，
        //必须用宽容整数读取，否则一条旧格式条目就让整个 ModPlayer 读档失败
        private static IEnumerable<(int RawSlot, string Key)> ReadTagEntries(List<TagCompound> list) {
            foreach (TagCompound entry in list) {
                if (entry == null || !entry.TrySafeGet("Key", out string key, nameof(OniMeiStore))) {
                    continue;
                }
                if (entry.TryGetAsInt("Slot", out int slot)) {
                    yield return (slot, key);
                }
            }
        }

        private static Dictionary<OniMeiSlotKind, string> Sanitize(
            IEnumerable<(int RawSlot, string Key)> entries) {
            Dictionary<OniMeiSlotKind, string> sanitized = [];
            foreach ((int rawSlot, string key) in entries) {
                if (rawSlot < (int)OniMeiSlotKind.Nakago
                    || rawSlot > (int)OniMeiSlotKind.Horimono
                    || string.IsNullOrWhiteSpace(key)) {
                    continue;
                }

                OniMeiSlotKind slot = (OniMeiSlotKind)rawSlot;
                if (sanitized.ContainsKey(slot)
                    || !OniMeiRegistry.TryGet(key, out OniMeiDefinition definition)
                    || definition.SlotKind != slot) {
                    continue;
                }
                sanitized.Add(slot, key);
            }
            return sanitized;
        }

        private void ReplaceWithSanitized(IEnumerable<(int RawSlot, string Key)> entries) {
            Dictionary<OniMeiSlotKind, string> sanitized = Sanitize(entries);
            slots.Clear();
            foreach (OniMeiSlotKind slot in slotKinds) {
                if (sanitized.TryGetValue(slot, out string key)) {
                    slots.Add(slot, key);
                }
            }
            BumpVersion();
        }
    }
}

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
        public const int SchemaVersion = 1;

        private static readonly OniMeiSlotKind[] slotKinds =
            [OniMeiSlotKind.Nakago, OniMeiSlotKind.Hi, OniMeiSlotKind.Horimono];

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
            slots.Clear();
            if (source != null) {
                foreach ((OniMeiSlotKind slot, string key) in source.slots) {
                    slots[slot] = key;
                }
            }
            BumpVersion();
        }

        public void Clear() {
            slots.Clear();
            BumpVersion();
        }

        /// <summary>存入宿主 tag，键带 OniMei 前缀</summary>
        public void SaveData(TagCompound tag) {
            List<TagCompound> list = [];
            foreach ((OniMeiSlotKind slot, string key) in slots) {
                if (string.IsNullOrEmpty(key)) {
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
            slots.Clear();
            BumpVersion();
            if (!tag.TryGet("OniMei:Slots", out List<TagCompound> list) || list == null) {
                return;
            }
            //schema 目前只有 1，读出备迁移
            tag.TryGet("OniMei:Version", out int _);
            foreach (TagCompound entry in list) {
                if (!entry.TryGet("Slot", out byte rawSlot) || !entry.TryGet("Key", out string key)) {
                    continue;
                }
                //消毒：越界铭位/空键/未注册铭（跨版本删档）一律丢弃
                if (rawSlot > (byte)OniMeiSlotKind.Horimono || string.IsNullOrEmpty(key)
                    || !OniMeiRegistry.TryGet(key, out _)) {
                    continue;
                }
                slots[(OniMeiSlotKind)rawSlot] = key;
            }
        }

        //====联机序列化====

        public void NetSend(BinaryWriter writer) {
            writer.Write((byte)slots.Count);
            foreach ((OniMeiSlotKind slot, string key) in slots) {
                writer.Write((byte)slot);
                writer.Write(key ?? string.Empty);
            }
        }

        public void NetReceive(BinaryReader reader) {
            slots.Clear();
            int count = reader.ReadByte();
            //CWRItem.NetReceive 链中段，按声明数读弃保流对齐，非法项丢弃
            for (int i = 0; i < count; i++) {
                byte rawSlot = reader.ReadByte();
                string key = reader.ReadString();
                if (rawSlot > (byte)OniMeiSlotKind.Horimono || string.IsNullOrEmpty(key)
                    || !OniMeiRegistry.TryGet(key, out _)) {
                    continue;
                }
                slots[(OniMeiSlotKind)rawSlot] = key;
            }
            BumpVersion();
        }
    }
}

using System.Collections.Generic;
using System.IO;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.Wraiths.Core
{
    /// <summary>单只厉鬼的动态进度记录，键外置于所属存储</summary>
    public sealed class WraithProgressRecord
    {
        /// <summary>绑定状态</summary>
        public WraithBindState State = WraithBindState.Unknown;
        /// <summary>驾驭度 0~1，仅 Bound 后有意义</summary>
        public float Mastery;
        /// <summary>累计遭遇次数</summary>
        public int EncounterCount;

        /// <summary>全默认值的记录不值得落档</summary>
        public bool IsDefault => State == WraithBindState.Unknown && Mastery <= 0f && EncounterCount <= 0;

        public TagCompound Save() {
            TagCompound tag = new() {
                ["State"] = (byte)State
            };
            if (Mastery > 0f) {
                tag["Mastery"] = Mastery;
            }
            if (EncounterCount > 0) {
                tag["Encounters"] = EncounterCount;
            }
            return tag;
        }

        public static WraithProgressRecord Load(TagCompound tag) {
            WraithProgressRecord record = new();
            if (tag.TryGet("State", out byte state)) {
                record.State = (WraithBindState)state;
            }
            if (tag.TryGet("Mastery", out float mastery)) {
                record.Mastery = mastery;
            }
            if (tag.TryGet("Encounters", out int encounters)) {
                record.EncounterCount = encounters;
            }
            return record;
        }
    }

    /// <summary>
    /// 稳定键到进度记录的通用容器，宿主无关：世界存档、玩家存档或
    /// LegendData（绑定层落地后）均可各自嵌入一份。读写自带 schema 版本
    /// </summary>
    public sealed class WraithProgressStore
    {
        /// <summary>当前 schema 版本，结构变更时递增并在 LoadData 里做迁移</summary>
        public const int SchemaVersion = 1;

        private readonly Dictionary<string, WraithProgressRecord> records = [];

        public IReadOnlyDictionary<string, WraithProgressRecord> Records => records;

        /// <summary>
        /// 变更版本号，展示层用它做脏检查缓存。经存储方法的修改自动自增；
        /// 直接改 <see cref="WraithProgressRecord"/> 字段后须手动 <see cref="BumpVersion"/>
        /// </summary>
        public int Version { get; private set; }

        public void BumpVersion() => Version++;

        public WraithProgressRecord GetOrCreate(string key) {
            if (!records.TryGetValue(key, out WraithProgressRecord record)) {
                record = new WraithProgressRecord();
                records[key] = record;
                BumpVersion();
            }
            return record;
        }

        public bool TryGet(string key, out WraithProgressRecord record) => records.TryGetValue(key, out record);

        /// <summary>登记一次遭遇：计数自增，未知升为已发现</summary>
        public void MarkEncounter(string key) {
            WraithProgressRecord record = GetOrCreate(key);
            record.EncounterCount++;
            if (record.State == WraithBindState.Unknown) {
                record.State = WraithBindState.Discovered;
            }
            BumpVersion();
        }

        public void Clear() {
            records.Clear();
            BumpVersion();
        }

        /// <summary>写入宿主 tag，键带 WraithProgress 前缀避免与宿主自身数据冲突</summary>
        public void SaveData(TagCompound tag) {
            List<TagCompound> list = [];
            foreach ((string key, WraithProgressRecord record) in records) {
                if (record.IsDefault) {
                    continue;
                }
                TagCompound entry = record.Save();
                entry["Key"] = key;
                list.Add(entry);
            }
            if (list.Count == 0) {
                return;
            }
            tag["WraithProgress:Version"] = SchemaVersion;
            tag["WraithProgress:Records"] = list;
        }

        public void LoadData(TagCompound tag) {
            records.Clear();
            BumpVersion();
            if (!tag.TryGet("WraithProgress:Records", out List<TagCompound> list) || list == null) {
                return;
            }
            //版本目前只有 1，读出来备着迁移分支
            tag.TryGet("WraithProgress:Version", out int _);
            foreach (TagCompound entry in list) {
                if (!entry.TryGet("Key", out string key) || string.IsNullOrEmpty(key) || records.ContainsKey(key)) {
                    continue;
                }
                records[key] = WraithProgressRecord.Load(entry);
            }
        }

        //====联机序列化（物品 NetSend/NetReceive 链使用，两端读写顺序一致）====

        public void NetSend(BinaryWriter writer) {
            writer.Write(records.Count);
            foreach ((string key, WraithProgressRecord record) in records) {
                writer.Write(key);
                writer.Write((byte)record.State);
                writer.Write(record.Mastery);
                writer.Write(record.EncounterCount);
            }
        }

        public void NetReceive(BinaryReader reader) {
            records.Clear();
            int count = reader.ReadInt32();
            //上限防御:恶意/损坏包不至于撑爆字典
            if (count < 0 || count > 512) {
                BumpVersion();
                return;
            }
            for (int i = 0; i < count; i++) {
                string key = reader.ReadString();
                WraithProgressRecord record = new() {
                    State = (WraithBindState)reader.ReadByte(),
                    Mastery = reader.ReadSingle(),
                    EncounterCount = reader.ReadInt32(),
                };
                if (!string.IsNullOrEmpty(key)) {
                    records[key] = record;
                }
            }
            BumpVersion();
        }
    }
}

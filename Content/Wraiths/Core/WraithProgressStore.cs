using System;
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
        /// <summary>
        /// 契约续签：亲手初铭或据点重续后为真。点鬼簿来历/赋力残页据此解锁——
        /// 出厂旧契"认刀不认手"，残页要自己挣（认主叙事，见 WRAITHS-DESIGN.md 第三节）
        /// </summary>
        public bool PactRenewed;

        /// <summary>全默认值的记录不值得落档</summary>
        public bool IsDefault => State == WraithBindState.Unknown && Mastery <= 0f && EncounterCount <= 0 && !PactRenewed;

        public WraithProgressRecord Clone() => new() {
            State = State,
            Mastery = Mastery,
            EncounterCount = EncounterCount,
            PactRenewed = PactRenewed,
        };

        public TagCompound Save() {
            TagCompound tag = new() {
                ["State"] = (byte)State
            };
            float mastery = WraithProgressStore.SanitizeMastery(Mastery);
            if (mastery > 0f) {
                tag["Mastery"] = mastery;
            }
            if (EncounterCount > 0) {
                tag["Encounters"] = EncounterCount;
            }
            if (PactRenewed) {
                tag["Renewed"] = true;
            }
            return tag;
        }

        public static WraithProgressRecord Load(TagCompound tag) {
            WraithProgressRecord record = new();
            if (tag.TryGet("State", out byte state)) {
                record.State = WraithProgressStore.SanitizeState(state);
            }
            if (tag.TryGet("Mastery", out float mastery)) {
                record.Mastery = WraithProgressStore.SanitizeMastery(mastery);
            }
            if (tag.TryGet("Encounters", out int encounters)) {
                record.EncounterCount = Math.Max(encounters, 0);
            }
            if (tag.TryGet("Renewed", out bool renewed)) {
                record.PactRenewed = renewed;
            }
            return record;
        }
    }

    /// <summary>
    /// 稳定键到进度记录的通用容器，宿主无关：世界存档、玩家存档或
    /// LegendData（绑定层落地后）均可各自嵌入一份。读写自带 schema 版本，
    /// 所有入口（存档往返与联机序列化）统一做数值消毒
    /// </summary>
    public sealed class WraithProgressStore
    {
        /// <summary>当前 schema 版本，结构变更时递增并在 LoadData 里做迁移</summary>
        public const int SchemaVersion = 1;

        /// <summary>联机反序列化保留的记录上限，防恶意/损坏包撑爆字典</summary>
        private const int NetRecordCap = 512;

        private readonly Dictionary<string, WraithProgressRecord> records = [];

        public IReadOnlyDictionary<string, WraithProgressRecord> Records => records;

        /// <summary>
        /// 变更版本号，展示层用它做脏检查缓存。经存储方法的修改自动自增；
        /// 直接改 <see cref="WraithProgressRecord"/> 字段后须手动 <see cref="BumpVersion"/>
        /// </summary>
        public int Version { get; private set; }

        public void BumpVersion() => Version++;

        //====数值消毒（存档/联机所有入口共用）====

        /// <summary>驾驭度消毒：NaN 归零，钳到 0~1</summary>
        internal static float SanitizeMastery(float value)
            => float.IsNaN(value) ? 0f : Math.Clamp(value, 0f, 1f);

        /// <summary>状态消毒：枚举范围外回落 Unknown</summary>
        internal static WraithBindState SanitizeState(byte raw)
            => raw <= (byte)WraithBindState.Sealed ? (WraithBindState)raw : WraithBindState.Unknown;

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

        /// <summary>
        /// 按注册表补种缺失定义的初始状态记录（跳过目录隐藏件）。只补缺失，绝不覆盖已有值——
        /// InitTag 老档回放语义不受影响；生来封印的鬼（井中鸣）由此在旧档上也封得住（鬼律兜底）
        /// </summary>
        public void SeedMissingStates() {
            foreach (WraithDefinition definition in WraithRegistry.All) {
                if (definition.HiddenFromCatalog || records.ContainsKey(definition.Key)) {
                    continue;
                }
                records[definition.Key] = new WraithProgressRecord { State = definition.InitialBindState };
            }
            BumpVersion();
        }

        /// <summary>深拷贝另一份容器的全部记录（物品克隆链使用），版本号自增</summary>
        public void CopyFrom(WraithProgressStore source) {
            records.Clear();
            if (source != null) {
                foreach ((string key, WraithProgressRecord record) in source.records) {
                    records[key] = record.Clone();
                }
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
                writer.Write(SanitizeMastery(record.Mastery));
                writer.Write(Math.Max(record.EncounterCount, 0));
                writer.Write(record.PactRenewed);
            }
        }

        public void NetReceive(BinaryReader reader) {
            records.Clear();
            int count = reader.ReadInt32();
            //本方法处于 CWRItem.NetReceive 链中段:无论计数多离谱都按声明数量读弃,
            //保持流对齐,绝不提前 return(负计数循环天然零次);超上限的记录读出即丢
            for (int i = 0; i < count; i++) {
                string key = reader.ReadString();
                WraithBindState state = SanitizeState(reader.ReadByte());
                float mastery = SanitizeMastery(reader.ReadSingle());
                int encounters = Math.Max(reader.ReadInt32(), 0);
                bool renewed = reader.ReadBoolean();
                if (i >= NetRecordCap || string.IsNullOrEmpty(key) || records.ContainsKey(key)) {
                    continue;
                }
                records[key] = new WraithProgressRecord {
                    State = state,
                    Mastery = mastery,
                    EncounterCount = encounters,
                    PactRenewed = renewed,
                };
            }
            BumpVersion();
        }
    }
}

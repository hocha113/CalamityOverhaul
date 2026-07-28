using System;
using System.Collections.Generic;
using System.IO;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.Wraiths.Core
{
    /// <summary>单鬼动态进度，键外置</summary>
    public sealed class WraithProgressRecord
    {
        /// <summary>绑定状态</summary>
        public WraithBindState State = WraithBindState.Unknown;
        /// <summary>驾驭度 0~1，仅 Bound 有意义</summary>
        public float Mastery;
        /// <summary>累计遭遇次数</summary>
        public int EncounterCount;
        /// <summary>亲手初铭或据点重续后为真，残页据此解锁</summary>
        public bool PactRenewed;

        /// <summary>全默认则不落档</summary>
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

    /// <summary>键→进度容器，宿主无关；存档与联机入口统一消毒</summary>
    public sealed class WraithProgressStore
    {
        /// <summary>schema 版本，结构变更递增</summary>
        public const int SchemaVersion = 2;

        private const string SaveKeyAttuned = "WraithProgress:Attuned";

        /// <summary>联机反序列化记录上限</summary>
        private const int NetRecordCap = 512;

        private readonly Dictionary<string, WraithProgressRecord> records = [];

        public IReadOnlyDictionary<string, WraithProgressRecord> Records => records;

        /// <summary>变更版本，展示层脏检查；直接改 Record 字段后须手动 Bump</summary>
        public int Version { get; private set; }

        /// <summary>当前共鸣之鬼；空字符串表示沿用自动选择</summary>
        public string AttunedKey { get; private set; } = string.Empty;

        public void BumpVersion() => Version++;

        /// <summary>仅 Bound 且有赋力的记录可被点鬼簿选作共鸣。</summary>
        public bool TryAttune(string key) {
            if (string.IsNullOrWhiteSpace(key)
                || !records.TryGetValue(key, out WraithProgressRecord record)
                || record.State != WraithBindState.Bound
                || !WraithRegistry.TryGet(key, out WraithDefinition definition)
                || definition.Ability == null) {
                return false;
            }
            if (AttunedKey == key) {
                return true;
            }
            AttunedKey = key;
            BumpVersion();
            return true;
        }

        /// <summary>旧定义改名迁移；新键已有非默认进度时保留新键。</summary>
        public bool MigrateKey(string oldKey, string newKey) {
            if (string.IsNullOrWhiteSpace(oldKey) || string.IsNullOrWhiteSpace(newKey)
                || oldKey == newKey || !records.TryGetValue(oldKey, out WraithProgressRecord oldRecord)) {
                return false;
            }

            if (!records.TryGetValue(newKey, out WraithProgressRecord newRecord) || newRecord.IsDefault
                || oldRecord.State == WraithBindState.Bound && newRecord.State != WraithBindState.Bound) {
                records[newKey] = oldRecord;
            }
            else if (oldRecord.State == WraithBindState.Bound && newRecord.State == WraithBindState.Bound) {
                newRecord.Mastery = Math.Max(newRecord.Mastery, oldRecord.Mastery);
                newRecord.EncounterCount = Math.Max(newRecord.EncounterCount, oldRecord.EncounterCount);
                newRecord.PactRenewed |= oldRecord.PactRenewed;
            }
            records.Remove(oldKey);
            if (AttunedKey == oldKey) {
                AttunedKey = newKey;
            }
            BumpVersion();
            return true;
        }

        private void SanitizeAttunedKey() {
            if (string.IsNullOrWhiteSpace(AttunedKey)
                || !records.TryGetValue(AttunedKey, out WraithProgressRecord record)
                || record.State != WraithBindState.Bound) {
                AttunedKey = string.Empty;
            }
        }

        //====数值消毒====

        /// <summary>驾驭度，NaN→0，钳 0~1</summary>
        internal static float SanitizeMastery(float value)
            => float.IsNaN(value) ? 0f : Math.Clamp(value, 0f, 1f);

        /// <summary>状态，越界→Unknown</summary>
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

        /// <summary>登记遭遇，未知升已发现</summary>
        public void MarkEncounter(string key) {
            WraithProgressRecord record = GetOrCreate(key);
            record.EncounterCount++;
            if (record.State == WraithBindState.Unknown) {
                record.State = WraithBindState.Discovered;
            }
            BumpVersion();
        }

        /// <summary>补种缺失定义初始态，不覆盖已有（生来封印旧档兜底）</summary>
        public void SeedMissingStates() {
            foreach (WraithDefinition definition in WraithRegistry.All) {
                if (definition.HiddenFromCatalog || records.ContainsKey(definition.Key)) {
                    continue;
                }
                records[definition.Key] = new WraithProgressRecord { State = definition.InitialBindState };
            }
            BumpVersion();
        }

        /// <summary>深拷贝，物品克隆链用</summary>
        public void CopyFrom(WraithProgressStore source) {
            records.Clear();
            AttunedKey = string.Empty;
            if (source != null) {
                foreach ((string key, WraithProgressRecord record) in source.records) {
                    records[key] = record.Clone();
                }
                AttunedKey = source.AttunedKey;
                SanitizeAttunedKey();
            }
            BumpVersion();
        }

        public void Clear() {
            records.Clear();
            AttunedKey = string.Empty;
            BumpVersion();
        }

        /// <summary>写入宿主 tag，键带 WraithProgress 前缀</summary>
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
            tag["WraithProgress:Version"] = SchemaVersion;
            if (list.Count > 0) {
                tag["WraithProgress:Records"] = list;
            }
            if (!string.IsNullOrEmpty(AttunedKey)) {
                tag[SaveKeyAttuned] = AttunedKey;
            }
        }

        public void LoadData(TagCompound tag) {
            records.Clear();
            AttunedKey = tag.TryGet(SaveKeyAttuned, out string attuned) ? attuned : string.Empty;
            BumpVersion();
            if (!tag.TryGet("WraithProgress:Records", out List<TagCompound> list) || list == null) {
                SanitizeAttunedKey();
                return;
            }
            tag.TryGet("WraithProgress:Version", out int _);
            foreach (TagCompound entry in list) {
                if (!entry.TryGet("Key", out string key) || string.IsNullOrEmpty(key) || records.ContainsKey(key)) {
                    continue;
                }
                records[key] = WraithProgressRecord.Load(entry);
            }
            SanitizeAttunedKey();
        }

        //====联机序列化====

        public void NetSend(BinaryWriter writer) {
            writer.Write(records.Count);
            foreach ((string key, WraithProgressRecord record) in records) {
                writer.Write(key);
                writer.Write((byte)record.State);
                writer.Write(SanitizeMastery(record.Mastery));
                writer.Write(Math.Max(record.EncounterCount, 0));
                writer.Write(record.PactRenewed);
            }
            writer.Write(AttunedKey ?? string.Empty);
        }

        public void NetReceive(BinaryReader reader) {
            records.Clear();
            AttunedKey = string.Empty;
            int count = reader.ReadInt32();
            //CWRItem.NetReceive 链中段，按声明数读弃保流对齐，超上限丢弃
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
            AttunedKey = reader.ReadString();
            SanitizeAttunedKey();
            BumpVersion();
        }
    }
}

using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.Wraiths.Runtime
{
    internal sealed class WraithScapeStateRecord
    {
        internal string PlayerKey;
        internal float Revival;
        internal int Multiplier = 2;
        internal int RevivalIdleTicks;

        internal TagCompound Save() => new() {
            ["Player"] = PlayerKey,
            ["Revival"] = Revival,
            ["Multiplier"] = Multiplier,
            ["IdleTicks"] = RevivalIdleTicks,
        };

        internal static WraithScapeStateRecord Load(TagCompound tag) {
            string key = tag.GetString("Player");
            float revival = tag.TryGet("Revival", out float storedRevival)
                && float.IsFinite(storedRevival) ? MathHelper.Clamp(storedRevival, 0f, 1f) : 0f;
            int multiplier = tag.TryGet("Multiplier", out int storedMultiplier)
                ? WraithPlayer.SanitizeScapeMultiplier(storedMultiplier) : 2;
            int idleTicks = tag.TryGet("IdleTicks", out int storedIdleTicks)
                ? Math.Clamp(storedIdleTicks, 0, WraithPlayer.RevivalDecayDelay) : 0;
            return new WraithScapeStateRecord {
                PlayerKey = key,
                Revival = revival,
                Multiplier = multiplier,
                RevivalIdleTicks = idleTicks,
            };
        }
    }

    /// <summary>
    /// 多人替死代价的世界侧权威账本。客户端角色存档不得反向初始化这里的值。
    /// </summary>
    public sealed class WraithScapeStateSystem : ModSystem
    {
        private const string SaveKey = "WraithScapePlayers";
        private static readonly Dictionary<string, WraithScapeStateRecord> records
            = new(StringComparer.OrdinalIgnoreCase);

        internal static bool TryGetOrCreate(Player player, out float revival, out int multiplier
            , out int revivalIdleTicks) {
            WraithScapeStateRecord record = GetOrCreateRecord(player);
            revival = record?.Revival ?? 0f;
            multiplier = record?.Multiplier ?? 2;
            revivalIdleTicks = record?.RevivalIdleTicks ?? 0;
            return record != null;
        }

        internal static void Set(Player player, float revival, int multiplier, int revivalIdleTicks) {
            WraithScapeStateRecord record = GetOrCreateRecord(player);
            if (record == null) {
                return;
            }
            record.Revival = float.IsFinite(revival) ? MathHelper.Clamp(revival, 0f, 1f) : 0f;
            record.Multiplier = WraithPlayer.SanitizeScapeMultiplier(multiplier);
            record.RevivalIdleTicks = Math.Clamp(revivalIdleTicks, 0, WraithPlayer.RevivalDecayDelay);
        }

        private static WraithScapeStateRecord GetOrCreateRecord(Player player) {
            string key = GetPlayerKey(player);
            if (string.IsNullOrEmpty(key)) {
                return null;
            }
            if (!records.TryGetValue(key, out WraithScapeStateRecord record)) {
                record = new WraithScapeStateRecord { PlayerKey = key };
                records[key] = record;
            }
            return record;
        }

        private static string GetPlayerKey(Player player) {
            string name = player?.name?.Trim();
            //原版协议没有向模组暴露服务器认证的角色 UUID，只能使用稳定的服侧可见名称。
            return string.IsNullOrEmpty(name) ? string.Empty : name.ToUpperInvariant();
        }

        public override void ClearWorld() => records.Clear();

        public override void SaveWorldData(TagCompound tag) {
            List<TagCompound> saved = [];
            foreach (WraithScapeStateRecord record in records.Values) {
                if (!string.IsNullOrEmpty(record.PlayerKey)) {
                    saved.Add(record.Save());
                }
            }
            if (saved.Count > 0) {
                tag[SaveKey] = saved;
            }
        }

        public override void LoadWorldData(TagCompound tag) {
            records.Clear();
            if (!tag.TryGet(SaveKey, out List<TagCompound> saved) || saved == null) {
                return;
            }
            foreach (TagCompound entry in saved) {
                WraithScapeStateRecord record = WraithScapeStateRecord.Load(entry);
                if (!string.IsNullOrWhiteSpace(record.PlayerKey)) {
                    records[record.PlayerKey] = record;
                }
            }
        }
    }
}

using CalamityOverhaul.Content.Wraiths.Core;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.Wraiths.Runtime
{
    /// <summary>单个据点的世界状态。锚位/事件计数随世界落档，冷却与在场跟踪为会话级</summary>
    public sealed class WraithSiteRecord
    {
        /// <summary>所属定义的稳定键</summary>
        public string Key;
        /// <summary>据点中心（世界坐标），Anchored 为真才有意义</summary>
        public Vector2 Anchor;
        /// <summary>已锚定：动态选点成功或被外部手工落锚</summary>
        public bool Anchored;
        /// <summary>已完结的据点事件次数</summary>
        public int EventCount;

        //====会话级（不落档）====
        /// <summary>再活化冷却到期的游戏帧</summary>
        public long CooldownUntil;
        /// <summary>下次尝试动态锚定的游戏帧</summary>
        public long NextAnchorRetry;
        /// <summary>本据点当前在场实体的 WhoAmI，-1=无事件进行</summary>
        public int ActiveWhoAmI = -1;
        /// <summary>在场实体的代标识，防槽位复用后误认</summary>
        public ushort ActiveGeneration;

        public TagCompound Save() => new() {
            ["Key"] = Key,
            ["AnchorX"] = Anchor.X,
            ["AnchorY"] = Anchor.Y,
            ["Anchored"] = Anchored,
            ["Events"] = EventCount,
        };

        public static WraithSiteRecord Load(TagCompound tag) {
            WraithSiteRecord record = new() {
                Key = tag.GetString("Key"),
            };
            if (tag.TryGet("AnchorX", out float x) && tag.TryGet("AnchorY", out float y)) {
                record.Anchor = new Vector2(x, y);
            }
            if (tag.TryGet("Anchored", out bool anchored)) {
                record.Anchored = anchored;
            }
            if (tag.TryGet("Events", out int events)) {
                record.EventCount = events;
            }
            return record;
        }
    }

    /// <summary>
    /// 据点锚状态宿主：键控记录 + 世界存档（镜像 <see cref="Core.WraithWorldProgress"/> 惯例）。
    /// 只在权威端有意义（客户端实体由生成广播带来，无需据点知识）；
    /// 活化调度在 <see cref="WraithDirector"/>
    /// </summary>
    public sealed class WraithSiteSystem : ModSystem
    {
        private static readonly Dictionary<string, WraithSiteRecord> records = [];

        public static IReadOnlyDictionary<string, WraithSiteRecord> Records => records;

        public static bool TryGet(string key, out WraithSiteRecord record) => records.TryGetValue(key, out record);

        internal static WraithSiteRecord GetOrCreate(string key) {
            if (!records.TryGetValue(key, out WraithSiteRecord record)) {
                record = new WraithSiteRecord { Key = key };
                records[key] = record;
            }
            return record;
        }

        /// <summary>
        /// 手工落锚（剧情/结构/调试路径），center 为据点中心；仅权威端有效。
        /// 已有锚的据点被移锚并清掉冷却，进行中的事件不受影响
        /// </summary>
        public static void Plant(string key, Vector2 center) {
            if (VaultUtils.isClient || string.IsNullOrEmpty(key)) {
                return;
            }
            WraithSiteRecord record = GetOrCreate(key);
            record.Anchor = center;
            record.Anchored = true;
            record.CooldownUntil = 0;
        }

        /// <summary>拔除据点锚（记录保留事件计数）；仅权威端有效</summary>
        public static void Unanchor(string key) {
            if (VaultUtils.isClient || !records.TryGetValue(key, out WraithSiteRecord record)) {
                return;
            }
            record.Anchored = false;
        }

        public override void ClearWorld() => records.Clear();

        public override void SaveWorldData(TagCompound tag) {
            List<TagCompound> list = [];
            foreach (WraithSiteRecord record in records.Values) {
                //从未锚定且无事件史的记录不值得落档
                if (!record.Anchored && record.EventCount <= 0) {
                    continue;
                }
                list.Add(record.Save());
            }
            if (list.Count > 0) {
                tag["WraithSites"] = list;
            }
        }

        public override void LoadWorldData(TagCompound tag) {
            records.Clear();
            if (!tag.TryGet("WraithSites", out List<TagCompound> list) || list == null) {
                return;
            }
            foreach (TagCompound entry in list) {
                WraithSiteRecord record = WraithSiteRecord.Load(entry);
                if (!string.IsNullOrEmpty(record.Key) && !records.ContainsKey(record.Key)) {
                    records[record.Key] = record;
                }
            }
        }
    }
}

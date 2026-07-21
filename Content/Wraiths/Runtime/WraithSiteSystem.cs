using CalamityOverhaul.Content.Wraiths.Core;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.Wraiths.Runtime
{
    /// <summary>据点世界状态；冷却与在场为会话级</summary>
    public sealed class WraithSiteRecord
    {
        /// <summary>所属定义的稳定键</summary>
        public string Key;
        /// <summary>据点中心（世界坐标），Anchored 为真才有意义</summary>
        public Vector2 Anchor;
        /// <summary>已锚定</summary>
        public bool Anchored;
        /// <summary>已完结的据点事件次数</summary>
        public int EventCount;

        //====会话级（不落档）====
        /// <summary>再活化冷却到期的游戏帧</summary>
        public long CooldownUntil;
        /// <summary>下次尝试动态锚定的游戏帧</summary>
        public long NextAnchorRetry;
        /// <summary>在场实体 WhoAmI，-1=无</summary>
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
    /// 据点锚宿主。权威真身；客户端 SiteSync 只读镜像；调度在 Director
    /// </summary>
    public sealed class WraithSiteSystem : ModSystem
    {
        private static readonly Dictionary<string, WraithSiteRecord> records = [];

        //====锚位镜像下发（服务器会话态）====
        //上次广播锚状态
        private static readonly Dictionary<string, (Vector2 anchor, bool anchored)> broadcastShadow = [];
        //已补发过全量快照的客户端槽位
        private static readonly bool[] snapshotSent = new bool[Main.maxPlayers];

        public static IReadOnlyDictionary<string, WraithSiteRecord> Records => records;

        public static bool TryGet(string key, out WraithSiteRecord record) => records.TryGetValue(key, out record);

        internal static WraithSiteRecord GetOrCreate(string key) {
            if (!records.TryGetValue(key, out WraithSiteRecord record)) {
                record = new WraithSiteRecord { Key = key };
                records[key] = record;
            }
            return record;
        }

        /// <summary>手工落锚，仅权威；不清冷却</summary>
        public static void Plant(string key, Vector2 center) {
            if (VaultUtils.isClient || string.IsNullOrEmpty(key)) {
                return;
            }
            WraithSiteRecord record = GetOrCreate(key);
            record.Anchor = center;
            record.Anchored = true;
        }

        /// <summary>清再活化冷却，仅权威</summary>
        public static void ResetCooldown(string key) {
            if (VaultUtils.isClient || !records.TryGetValue(key, out WraithSiteRecord record)) {
                return;
            }
            record.CooldownUntil = 0;
        }

        /// <summary>拔锚，仅权威</summary>
        public static void Unanchor(string key) {
            if (VaultUtils.isClient || !records.TryGetValue(key, out WraithSiteRecord record)) {
                return;
            }
            record.Anchored = false;
        }

        /// <summary>客户端套用锚位镜像</summary>
        internal static void ApplyClientMirror(string key, Vector2 anchor, bool anchored) {
            if (!VaultUtils.isClient) {
                return;
            }
            WraithSiteRecord record = GetOrCreate(key);
            record.Anchor = anchor;
            record.Anchored = anchored;
        }

        /// <summary>服侧锚位变更广播，新客户端补全量</summary>
        public override void PostUpdateEverything() {
            if (!VaultUtils.isServer) {
                return;
            }
            foreach ((string key, WraithSiteRecord record) in records) {
                bool changed = !broadcastShadow.TryGetValue(key, out var shadow)
                    || shadow.anchored != record.Anchored || shadow.anchor != record.Anchor;
                if (changed) {
                    broadcastShadow[key] = (record.Anchor, record.Anchored);
                    WraithNet.SendSiteSync(key, record.Anchor, record.Anchored);
                }
            }
            for (int i = 0; i < Main.maxPlayers; i++) {
                bool online = Netplay.Clients[i].State == 10;
                if (!online) {
                    snapshotSent[i] = false;
                    continue;
                }
                if (snapshotSent[i]) {
                    continue;
                }
                snapshotSent[i] = true;
                foreach ((string key, WraithSiteRecord record) in records) {
                    WraithNet.SendSiteSync(key, record.Anchor, record.Anchored, i);
                }
            }
        }

        public override void ClearWorld() {
            records.Clear();
            broadcastShadow.Clear();
            Array.Clear(snapshotSent);
        }

        public override void SaveWorldData(TagCompound tag) {
            List<TagCompound> list = [];
            foreach (WraithSiteRecord record in records.Values) {
                //未锚定且无事件史不落档
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

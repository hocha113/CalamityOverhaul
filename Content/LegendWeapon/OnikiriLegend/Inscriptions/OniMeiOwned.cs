using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.Inscriptions
{
    /// <summary>
    /// 所持铭库门闩：玩家已获得、可在改铭台扇骨出现的 Key 集合。<br/>
    /// 在凿仍走 <see cref="OniMeiStore"/>；本类不写刀槽。<br/>
    /// 过渡期：仅核 7 + 鬼切出厂所持；扩册 15 变体只靠 Unlock / 錾样入包
    /// </summary>
    internal static class OniMeiOwned
    {
        public static string SeedKey => nameof(MeiOnikiri);

        /// <summary>出厂所持白名单，不随 Registry 自动扩；新铭勿写入此表</summary>
        private static readonly string[] DefaultOwnedKeys = [
            nameof(MeiOnikiri),
            nameof(MeiHigekiri),
            nameof(MeiShishinoko),
            nameof(MeiTomokiri),
            nameof(MeiKazehi),
            nameof(MeiChihi),
            nameof(MeiFudo),
            nameof(MeiKurikara),
        ];

        /// <summary>是否出厂所持；刀縁注册期据此挡住"给白名单铭又配縁"的设计错</summary>
        internal static bool IsDefaultOwned(string key) {
            if (string.IsNullOrEmpty(key)) {
                return false;
            }
            foreach (string owned in DefaultOwnedKeys) {
                if (owned == key) {
                    return true;
                }
            }
            return false;
        }

        public static bool Owns(Player player, string key) {
            if (player == null || string.IsNullOrEmpty(key)) {
                return false;
            }
            if (!player.TryGetModPlayer(out OnikiriPlayer okp)) {
                return false;
            }
            EnsureSeed(okp);
            return okp.OwnedMeiKeys.Contains(key);
        }

        /// <summary>写入所持；已有则 false</summary>
        public static bool Unlock(Player player, string key) {
            if (player == null || string.IsNullOrEmpty(key)) {
                return false;
            }
            if (!player.TryGetModPlayer(out OnikiriPlayer okp)) {
                return false;
            }
            EnsureSeed(okp);
            bool added = okp.OwnedMeiKeys.Add(key);
            if (added) {
                OnikiriNet.SendOwnedMeiSnapshot(player);
            }
            return added;
        }

        internal static void ApplyNetworkSnapshot(Player player, IEnumerable<string> keys) {
            if (player == null || !player.TryGetModPlayer(out OnikiriPlayer okp)) {
                return;
            }
            okp.OwnedMeiKeys = [];
            if (keys != null) {
                foreach (string key in keys) {
                    if (OniMeiRegistry.TryGet(key, out _)) {
                        okp.OwnedMeiKeys.Add(key);
                    }
                }
            }
            EnsureSeed(okp);
        }

        public static void EnsureSeed(OnikiriPlayer okp) {
            if (okp == null) {
                return;
            }
            okp.OwnedMeiKeys ??= [];
            foreach (string key in DefaultOwnedKeys) {
                okp.OwnedMeiKeys.Add(key);
            }
        }

        /// <summary>某槽扇骨候选：名册 ∩ 所持，保持 SortOrder</summary>
        public static List<OniMeiDefinition> GetBySlotOwned(OniMeiSlotKind slot, Player player) {
            List<OniMeiDefinition> list = [];
            foreach (OniMeiDefinition definition in OniMeiRegistry.GetBySlot(slot)) {
                if (Owns(player, definition.Key)) {
                    list.Add(definition);
                }
            }
            return list;
        }

        /// <summary>
        /// 某槽扇骨候选（含未凿位）：已持在前保持 SortOrder，之后接尚有刀縁可循的未凿铭。<br/>
        /// 无縁又未持的铭（如 Boss 赠礼未领）不上扇，免得挂一根点不动的死骨
        /// </summary>
        public static List<(OniMeiDefinition Definition, bool Owned)> GetBySlotWithLocked(
            OniMeiSlotKind slot, Player player) {
            List<(OniMeiDefinition, bool)> list = [];
            List<OniMeiDefinition> locked = [];
            foreach (OniMeiDefinition definition in OniMeiRegistry.GetBySlot(slot)) {
                if (Owns(player, definition.Key)) {
                    list.Add((definition, true));
                }
                else if (Deeds.OniMeiDeedRegistry.TryGetByMei(definition.Key, out _)) {
                    locked.Add(definition);
                }
            }
            foreach (OniMeiDefinition definition in locked) {
                list.Add((definition, false));
            }
            return list;
        }
    }
}

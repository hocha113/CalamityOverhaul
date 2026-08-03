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
    }
}

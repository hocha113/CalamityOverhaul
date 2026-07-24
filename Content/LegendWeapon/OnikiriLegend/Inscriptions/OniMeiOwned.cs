using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.Inscriptions
{
    /// <summary>
    /// 所持铭库门闩：玩家已获得、可在改铭台扇骨出现的 Key 集合。<br/>
    /// 在凿仍走 <see cref="OniMeiStore"/>；本类不写刀槽。<br/>
    /// 现有名册 8 铭出厂默认所持（过渡期）；日后新铭只靠 Unlock / 錾样入包
    /// </summary>
    internal static class OniMeiOwned
    {
        public static string SeedKey => nameof(MeiOnikiri);

        /// <summary>现有名册出厂全开，不随 Registry 自动扩；新铭勿写入此表</summary>
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
            return okp.OwnedMeiKeys.Add(key);
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

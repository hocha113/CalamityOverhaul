using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaTalismans
{
    /// <summary>
    /// 符箧门闩：玩家已录入、可在祈雨绳候选上出现的 Key 集合。<br/>
    /// 上绳仍走 <see cref="KikasaTalismanStore"/>；本类不写符位。<br/>
    /// 无出厂白名单：符纸全靠物品入包 <see cref="Unlock"/> 录入。<br/>
    /// owner 本机数据，无需同步：挂符校验与符箧展示都只发生在本机
    /// （服务器仲裁挂符的时代需要一份镜像，符位表迁玩家侧后该消费点已不存在）
    /// </summary>
    internal static class KikasaTalismanOwned
    {
        public static bool Owns(Player player, string key) {
            if (player == null || string.IsNullOrEmpty(key)) {
                return false;
            }
            if (!player.TryGetModPlayer(out KikasaTalismanPlayer ktp)) {
                return false;
            }
            EnsureInit(ktp);
            return ktp.OwnedTalismanKeys.Contains(key);
        }

        /// <summary>写入符箧；已有则 false</summary>
        public static bool Unlock(Player player, string key) {
            if (player == null || string.IsNullOrEmpty(key)
                || !KikasaTalismanRegistry.TryGet(key, out _)) {
                return false;
            }
            if (!player.TryGetModPlayer(out KikasaTalismanPlayer ktp)) {
                return false;
            }
            EnsureInit(ktp);
            return ktp.OwnedTalismanKeys.Add(key);
        }

        public static void EnsureInit(KikasaTalismanPlayer ktp) {
            if (ktp != null) {
                ktp.OwnedTalismanKeys ??= [];
            }
        }

        /// <summary>符箧候选：注册表 ∩ 所持，保持 SortOrder</summary>
        public static List<KikasaTalismanDefinition> GetOwnedOrdered(Player player) {
            List<KikasaTalismanDefinition> list = [];
            foreach (KikasaTalismanDefinition definition in KikasaTalismanRegistry.All) {
                if (Owns(player, definition.Key)) {
                    list.Add(definition);
                }
            }
            return list;
        }
    }
}

using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.Inscriptions;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.UI
{
    /// <summary>錾样匣一格:背包聚合后的 Key + 堆叠</summary>
    internal readonly struct OniMeiTrayEntry(string key, int stack, bool gold)
    {
        public readonly string Key = key;
        public readonly int Stack = stack;
        public readonly bool Gold = gold;
    }

    /// <summary>
    /// 錾样匣数据缝:扫包 / 消耗 / 退样。无绘制
    /// </summary>
    internal static class OniMeiTrayLogic
    {
        /// <summary>背包(+鼠袋)中属于该槽的錾样,按 Key 合堆叠,SortOrder 序</summary>
        public static List<OniMeiTrayEntry> CollectForSlot(Player player, OniMeiSlotKind slot) {
            Dictionary<string, int> stacks = [];
            if (player == null) {
                return [];
            }

            void Consider(Item item) {
                if (item == null || item.IsAir || item.ModItem is not OniMeiRubbingItem rubbing) {
                    return;
                }
                if (!OniMeiRegistry.TryGet(rubbing.MeiKey, out OniMeiDefinition def) || def.SlotKind != slot) {
                    return;
                }
                stacks.TryGetValue(rubbing.MeiKey, out int n);
                stacks[rubbing.MeiKey] = n + item.stack;
            }

            for (int i = 0; i < player.inventory.Length; i++) {
                Consider(player.inventory[i]);
            }
            Consider(Main.mouseItem);

            List<OniMeiTrayEntry> list = [];
            foreach (OniMeiDefinition def in OniMeiRegistry.GetBySlot(slot)) {
                if (stacks.TryGetValue(def.Key, out int stack) && stack > 0) {
                    list.Add(new OniMeiTrayEntry(def.Key, stack, def.IsGoldTier));
                }
            }
            return list;
        }

        /// <summary>消耗 1 个指定 Key 錾样;失败 false</summary>
        public static bool TryConsume(Player player, string key) {
            if (player == null || string.IsNullOrEmpty(key)) {
                return false;
            }

            if (TryConsumeFrom(Main.mouseItem, key)) {
                return true;
            }
            for (int i = 0; i < player.inventory.Length; i++) {
                if (TryConsumeFrom(player.inventory[i], key)) {
                    return true;
                }
            }
            return false;
        }

        private static bool TryConsumeFrom(Item item, string key) {
            if (item == null || item.IsAir || item.ModItem is not OniMeiRubbingItem rubbing) {
                return false;
            }
            if (rubbing.MeiKey != key) {
                return false;
            }
            item.stack--;
            if (item.stack <= 0) {
                item.TurnToAir();
            }
            return true;
        }

        /// <summary>退 1 个錾样入包;无对应物品 Type 则 false</summary>
        public static bool TryRefund(Player player, string key) {
            if (player == null || string.IsNullOrEmpty(key)) {
                return false;
            }
            int type = OniMeiRubbingItem.ItemTypeForKey(key);
            if (type <= 0) {
                return false;
            }
            player.QuickSpawnItem(player.GetSource_Misc("OniMeiTrayRefund"), type, 1);
            return true;
        }
    }
}

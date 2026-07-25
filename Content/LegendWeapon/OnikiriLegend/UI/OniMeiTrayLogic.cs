using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.Inscriptions;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.UI
{
    /// <summary>錾样匣一格:背包中的 Key（可合计数）</summary>
    internal readonly struct OniMeiTrayEntry(string key, int stack, bool gold)
    {
        public readonly string Key = key;
        public readonly int Stack = stack;
        public readonly bool Gold = gold;
    }

    /// <summary>
    /// 錾样匣数据缝:扫包 / 持有判定。拓本不消耗、不退样。无绘制
    /// </summary>
    internal static class OniMeiTrayLogic
    {
        /// <summary>背包(+鼠袋)中属于该槽的錾样,按 Key 合计,SortOrder 序</summary>
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

        /// <summary>行囊(+鼠袋)是否持有指定 Key 錾样</summary>
        public static bool Has(Player player, string key) {
            if (player == null || string.IsNullOrEmpty(key)) {
                return false;
            }
            if (IsKey(Main.mouseItem, key)) {
                return true;
            }
            for (int i = 0; i < player.inventory.Length; i++) {
                if (IsKey(player.inventory[i], key)) {
                    return true;
                }
            }
            return false;
        }

        private static bool IsKey(Item item, string key)
            => item != null && !item.IsAir && item.ModItem is OniMeiRubbingItem rubbing && rubbing.MeiKey == key;
    }
}

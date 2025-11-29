using System.Collections.Generic;
using Terraria;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;

namespace CalamityOverhaul.Content.ADV.Scenarios.Abysses.OldDukes.OldDukeShops
{
    internal class OldDukeShopHandle
    {
        /// <summary>
        /// 处理商店物品列表
        /// </summary>
        public static void Handle(List<OldDukeShopItem> shopItems) {
            //海洋武器
            shopItems.Add(new OldDukeShopItem(ItemID.Trident, 1, 15));
            shopItems.Add(new OldDukeShopItem(ItemID.FrostStaff, 1, 25));
            shopItems.Add(new OldDukeShopItem(ItemID.BubbleGun, 1, 30));
            shopItems.Add(new OldDukeShopItem(ItemID.Flairon, 1, 45));
            shopItems.Add(new OldDukeShopItem(ItemID.TsunamiInABottle, 1, 20));

            //海洋装备
            shopItems.Add(new OldDukeShopItem(ItemID.SharkToothNecklace, 1, 12));
            shopItems.Add(new OldDukeShopItem(ItemID.DivingHelmet, 1, 8));
            shopItems.Add(new OldDukeShopItem(ItemID.DivingGear, 1, 18));
            shopItems.Add(new OldDukeShopItem(ItemID.JellyfishNecklace, 1, 15));

            //海洋材料
            shopItems.Add(new OldDukeShopItem(ItemID.Coral, 10, 5));
            shopItems.Add(new OldDukeShopItem(ItemID.Starfish, 5, 3));
            shopItems.Add(new OldDukeShopItem(ItemID.Seashell, 10, 2));
            shopItems.Add(new OldDukeShopItem(ItemID.SharkFin, 3, 8));

            //海洋药水
            shopItems.Add(new OldDukeShopItem(ItemID.GillsPotion, 5, 4));
            shopItems.Add(new OldDukeShopItem(ItemID.SonarPotion, 5, 3));
            shopItems.Add(new OldDukeShopItem(ItemID.WaterWalkingPotion, 5, 3));

            //稀有物品
            shopItems.Add(new OldDukeShopItem(ItemID.ReefBlock, 99, 2));
            shopItems.Add(new OldDukeShopItem(ItemID.Seaweed, 20, 1));

            var rareDrops = GetNPCDrops(CWRID.NPC_OldDuke, true);
            foreach (var id in rareDrops) {
                Item item = new Item(id);
                shopItems.Add(new OldDukeShopItem(id, 1, (int)MathHelper.Clamp(item.value / 6000, 1, 9999)));
            }
        }

        /// <summary>
        /// 获取指定NPC的所有掉落物品ID
        /// </summary>
        /// <param name="npcId">NPC的ID</param>
        /// <param name="includeGlobalRules">是否包含全局掉落规则</param>
        /// <returns>掉落物品ID的集合</returns>
        public static HashSet<int> GetNPCDrops(int npcId, bool includeGlobalRules = false) {
            HashSet<int> drops = new HashSet<int>();

            // 从游戏数据库获取该NPC的掉落规则
            List<IItemDropRule> dropRules = Main.ItemDropsDB.GetRulesForNPCID(npcId, includeGlobalRules);

            // 创建掉落率信息列表
            List<DropRateInfo> dropRateInfoList = new List<DropRateInfo>();
            DropRateInfoChainFeed ratesInfo = new DropRateInfoChainFeed(1f);

            // 解析每个掉落规则
            foreach (IItemDropRule rule in dropRules) {
                rule.ReportDroprates(dropRateInfoList, ratesInfo);
            }

            // 收集所有物品ID
            foreach (DropRateInfo dropRateInfo in dropRateInfoList) {
                drops.Add(dropRateInfo.itemId);
            }

            return drops;
        }
    }
}

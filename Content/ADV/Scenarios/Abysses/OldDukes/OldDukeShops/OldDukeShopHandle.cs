using System.Collections.Generic;
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
        }
    }
}

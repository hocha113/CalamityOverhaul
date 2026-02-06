using CalamityOverhaul.Content.ADV.Scenarios.Abysses.OldDukes.OceanRaiderses;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Abysses.OldDukes.OldDukeShops
{
    internal class OldDukeShopHandle
    {
        /// <summary>
        /// 处理商店物品列表
        /// </summary>
        public static void Handle(List<OldDukeShopItem> shopItems) {
            //鱼人钓
            shopItems.Add(new OldDukeShopItem(ModContent.ItemType<MermanRod>(), 1, 1));

            if (Main.LocalPlayer.Alives() && Main.LocalPlayer.TryGetADVSave(out var save) && save.OldDukeFindFragmentsQuestCompleted) {
                //海洋吞噬者
                shopItems.Add(new OldDukeShopItem(ModContent.ItemType<OceanRaiders>(), 1, 220));
            }

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

            //根据进度解锁
            if (InWorldBossPhase.Downed23.Invoke() || InWorldBossPhase.Downed26.Invoke()) {
                var rareDrops = VaultUtils.GetNPCDrops(CWRID.NPC_OldDuke, true);
                foreach (var id in rareDrops) {
                    Item item = new Item(id);
                    int value = (int)MathHelper.Clamp(item.value / 6000, 1, 9999);
                    if (value <= 3) {
                        continue;
                    }
                    shopItems.Add(new OldDukeShopItem(id, 1, value));
                }
            }
        }
    }
}

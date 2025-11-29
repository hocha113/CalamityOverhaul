using Terraria;

namespace CalamityOverhaul.Content.ADV.Scenarios.Abysses.OldDukes.OldDukeShops
{
    /// <summary>
    /// 老公爵商店物品数据
    /// </summary>
    public class OldDukeShopItem
    {
        public int itemType;
        public int stack;
        public int price;//海洋残片数量

        public OldDukeShopItem(int itemType, int stack, int price) {
            Main.instance.LoadItem(itemType);
            this.itemType = itemType;
            this.stack = stack;
            this.price = price;
        }
    }
}

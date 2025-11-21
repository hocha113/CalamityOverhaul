using Terraria;

namespace CalamityOverhaul.Content.ADV.Scenarios.Draedons.PQCDs.DraedonShops
{
    /// <summary>
    /// 商店物品数据
    /// </summary>
    public class ShopItem
    {
        public int itemType;
        public int stack;
        public int price;

        public ShopItem(int itemType, int stack, int price) {
            Main.instance.LoadItem(itemType);
            this.itemType = itemType;
            this.stack = stack;
            this.price = price;
        }
    }
}

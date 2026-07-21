using Terraria;

namespace CalamityOverhaul.Content.Scenarios.Draedon.PQCDs.DraedonShops
{
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

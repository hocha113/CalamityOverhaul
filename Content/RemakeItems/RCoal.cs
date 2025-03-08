using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.RemakeItems
{
    internal class RCoal : ItemOverride
    {
        public override int TargetID => ItemID.Coal;
        public override bool DrawingInfo => false;
        public override void SetDefaults(Item item) {
            item.maxStack = 9999;
            item.value = Item.buyPrice(0, 0, 0, 15);
        }
    }
}

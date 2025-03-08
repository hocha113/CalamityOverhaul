using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.RemakeItems
{
    internal class RCoal : ItemOverride
    {
        public override int TargetID => ItemID.Coal;
        public override void SetDefaults(Item item) {
            item.maxStack = 9999;
        }
    }
}

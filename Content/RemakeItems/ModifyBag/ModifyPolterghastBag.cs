using CalamityOverhaul.Content.Items.Summon;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.ModifyBag
{
    internal class ModifyPolterghastBag : BaseModifyBag
    {
        public override int TargetID => CWRID.Item_PolterghastBag;
        public override void ModifyItemLoot(Item item, ItemLoot itemLoot) {
            itemLoot.SimpleAdd(ModContent.ItemType<GhostFireWhip>(), 4);
        }
    }
}

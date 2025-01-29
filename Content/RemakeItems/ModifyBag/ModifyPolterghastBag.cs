using CalamityMod;
using CalamityMod.Items.TreasureBags;
using CalamityOverhaul.Content.Items.Summon;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.ModifyBag
{
    internal class ModifyPolterghastBag : BaseModifyBag
    {
        public override int TargetID => ModContent.ItemType<PolterghastBag>();
        public override void ModifyItemLoot(Item item, ItemLoot itemLoot) {
            itemLoot.Add(ModContent.ItemType<GhostFireWhip>(), 4);
        }
    }
}

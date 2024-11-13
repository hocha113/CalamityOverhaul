using CalamityMod;
using CalamityMod.Items.TreasureBags.MiscGrabBags;
using CalamityOverhaul.Content.Items;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.ModifyBag
{
    internal class ModifyStarterBag : BaseModifyBag
    {
        public override int TargetID => ModContent.ItemType<StarterBag>();
        public override void ModifyItemLoot(Item item, ItemLoot itemLoot) {
            itemLoot.Add(ModContent.ItemType<OverhaulTheBibleBook>());
        }
    }
}

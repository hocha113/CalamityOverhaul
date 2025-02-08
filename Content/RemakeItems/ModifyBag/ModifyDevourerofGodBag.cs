using CalamityMod;
using CalamityMod.Items.TreasureBags;
using CalamityMod.Items.Weapons.Melee;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.ModifyBag
{
    internal class ModifyDevourerofGodBag : BaseModifyBag
    {
        public override int TargetID => ModContent.ItemType<DevourerofGodsBag>();
        public override void ModifyItemLoot(Item item, ItemLoot itemLoot) {
            //itemLoot.Add(ModContent.ItemType<Ataraxia>(), 4);
            //itemLoot.Add(ModContent.ItemType<Nadir>(), 4);
        }
    }
}

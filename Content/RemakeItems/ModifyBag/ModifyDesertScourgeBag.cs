using CalamityMod.Items.TreasureBags;
using CalamityMod.Items.Weapons.Melee;
using Terraria.ModLoader;
using Terraria;
using CalamityMod;

namespace CalamityOverhaul.Content.RemakeItems.ModifyBag
{
    internal class ModifyDesertScourgeBag : BaseModifyBag
    {
        public override int TargetID => ModContent.ItemType<DesertScourgeBag>();
        public override void ModifyItemLoot(Item item, ItemLoot itemLoot) {
            itemLoot.Add(ModContent.ItemType<BurntSienna>(), 10);
        }
    }
}

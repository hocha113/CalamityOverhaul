using CalamityMod;
using CalamityMod.Items.TreasureBags;
using CalamityMod.Items.Weapons.Melee;
using CalamityOverhaul.Content.Items.Melee.Extras;
using CalamityOverhaul.Content.Items.Rogue.Extras;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.ModifyBag
{
    internal class ModifyDesertScourgeBag : BaseModifyBag
    {
        public override int TargetID => ModContent.ItemType<DesertScourgeBag>();
        public override void ModifyItemLoot(Item item, ItemLoot itemLoot) {
            itemLoot.Add(ModContent.ItemType<WastelandFang>(), 6);
            itemLoot.Add(ModContent.ItemType<SandDagger>(), 6);
            itemLoot.Add(ModContent.ItemType<BurntSienna>(), 10);
        }
    }
}

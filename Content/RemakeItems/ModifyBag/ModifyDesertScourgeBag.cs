using CalamityMod;
using CalamityMod.Items.TreasureBags;
using CalamityMod.Items.Weapons.Melee;
using CalamityOverhaul.Content.Items.Magic.Extras;
using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.Items.Rogue;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.ModifyBag
{
    internal class ModifyDesertScourgeBag : BaseModifyBag
    {
        public override int TargetID => ModContent.ItemType<DesertScourgeBag>();
        public override void ModifyItemLoot(Item item, ItemLoot itemLoot) {
            itemLoot.Add(ModContent.ItemType<UnderTheSand>(), 10);
            itemLoot.Add(ModContent.ItemType<WastelandFang>(), 10);
            itemLoot.Add(ModContent.ItemType<SandDagger>(), 10);
            itemLoot.Add(ModContent.ItemType<BurntSienna>(), 10);
        }
    }
}

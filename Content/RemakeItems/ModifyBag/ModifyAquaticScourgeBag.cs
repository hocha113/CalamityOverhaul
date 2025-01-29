using CalamityMod;
using CalamityMod.Items.TreasureBags;
using CalamityOverhaul.Content.Items.Magic;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.ModifyBag
{
    internal class ModifyAquaticScourgeBag : BaseModifyBag
    {
        public override int TargetID => ModContent.ItemType<AquaticScourgeBag>();
        public override void ModifyItemLoot(Item item, ItemLoot itemLoot) {
            itemLoot.Add(ModContent.ItemType<MelodyTheSand>(), 6);
        }
    }
}

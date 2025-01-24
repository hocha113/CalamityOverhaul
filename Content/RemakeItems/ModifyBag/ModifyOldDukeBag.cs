using CalamityMod;
using CalamityMod.Items.TreasureBags;
using CalamityOverhaul.Content.Items.Magic.Extras;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.ModifyBag
{
    internal class ModifyOldDukeBag : BaseModifyBag
    {
        public override int TargetID => ModContent.ItemType<OldDukeBag>();
        public override void ModifyItemLoot(Item item, ItemLoot itemLoot) {
            itemLoot.Add(ModContent.ItemType<SandVortexOfTheDecayedSea>(), 6);
        }
    }
}

using CalamityOverhaul.Content.Items.Magic;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.ModifyBag
{
    internal class ModifyOldDukeBag : BaseModifyBag
    {
        public override int TargetID => CWRID.Item_OldDukeBag;
        public override void ModifyItemLoot(Item item, ItemLoot itemLoot) {
            itemLoot.SimpleAdd(ModContent.ItemType<SandVortexOfTheDecayedSea>(), 6);
        }
    }
}

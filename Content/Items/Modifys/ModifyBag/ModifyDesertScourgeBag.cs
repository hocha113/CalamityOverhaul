using CalamityOverhaul.Content.Items.Magic;
using CalamityOverhaul.Content.Items.Melee;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Modifys.ModifyBag
{
    internal class ModifyDesertScourgeBag : BaseModifyBag
    {
        public override int TargetID => CWRID.Item_DesertScourgeBag;
        public override void ModifyItemLoot(Item item, ItemLoot itemLoot) {
            itemLoot.SimpleAdd(ModContent.ItemType<UnderTheSand>(), 10);
            itemLoot.SimpleAdd(ModContent.ItemType<WastelandFang>(), 10);
            itemLoot.SimpleAdd(ModContent.ItemType<SandDagger>(), 10);
            itemLoot.SimpleAdd(CWRID.Item_BurntSienna, 10);
        }
    }
}

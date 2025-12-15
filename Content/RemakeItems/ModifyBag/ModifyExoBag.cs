using CalamityOverhaul.Content.Items.Ranged;
using Terraria;
using Terraria.GameContent.ItemDropRules;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.ModifyBag
{
    internal class ModifyExoBag : BaseModifyBag
    {
        public override int TargetID => CWRID.Item_DraedonBag;
        public override void ModifyItemLoot(Item item, ItemLoot itemLoot) {
            IItemDropRule conditionalRule = new LeadingConditionRule(new Drop_Thanatos_Down());
            conditionalRule.OnSuccess(ItemDropRule.Common(ModContent.ItemType<MG42>()));
            itemLoot.Add(conditionalRule);
        }
    }
}

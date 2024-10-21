using CalamityMod.Items.Materials;
using CalamityMod.Items.TreasureBags;
using Terraria;
using Terraria.GameContent.ItemDropRules;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.ModifyBag
{
    internal class ModifyYharonBag : BaseModifyBag
    {
        public override int TargetID => ModContent.ItemType<YharonBag>();
        public override void ModifyItemLoot(Item item, ItemLoot itemLoot) {
            IItemDropRule conditionalRule = new LeadingConditionRule(new DropRule_Yharon_Down());
            IItemDropRule rule = ItemDropRule.Common(ModContent.ItemType<AuricBar>(), 1, 50, 80);
            conditionalRule.OnSuccess(rule);
            itemLoot.Add(conditionalRule);
        }
    }
}

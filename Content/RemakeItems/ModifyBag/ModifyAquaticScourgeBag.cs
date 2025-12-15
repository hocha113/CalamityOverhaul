using CalamityOverhaul.Content.Items.Magic;
using Terraria;
using Terraria.GameContent.ItemDropRules;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.ModifyBag
{
    internal class ModifyAquaticScourgeBag : BaseModifyBag
    {
        public override int TargetID => CWRID.Item_AquaticScourgeBag;
        public override void ModifyItemLoot(Item item, ItemLoot itemLoot) {
            itemLoot.Add(ItemDropRule.Common(ModContent.ItemType<MelodyTheSand>(), 6, 1, 1));
        }
    }
}

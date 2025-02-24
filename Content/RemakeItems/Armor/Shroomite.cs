using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.RemakeItems.Armor
{
    internal class RShroomiteMask : ItemOverride
    {
        public override int TargetID => ItemID.ShroomiteMask;
        public override bool DrawingInfo => false;
        public override void UpdateArmorByHead(Player player, Item body, Item legs) {
            if (body.type == ItemID.ShroomiteBreastplate && legs.type == ItemID.ShroomiteLeggings) {
                player.setBonus += "\n弹药装填时间减少20%";
                player.CWR().KreloadTimeIncrease -= 0.2f;
            }
        }
    }
}

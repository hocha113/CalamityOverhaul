using CalamityOverhaul.Common;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.RemakeItems.Armor
{
    internal class ModifyShroomiteMask : ItemOverride
    {
        public override int TargetID => ItemID.ShroomiteMask;
        public override bool DrawingInfo => false;
        public override void UpdateArmorByHead(Player player, Item body, Item legs) => UpdateArmor(player, body, legs);

        public static void UpdateArmor(Player player, Item body, Item legs) {
            if (body.type != ItemID.ShroomiteBreastplate || legs.type != ItemID.ShroomiteLeggings) {
                return;
            }
            player.setBonus += "\n" + CWRLocText.Instance.KreloadTimeLessenText + "20%";
            player.CWR().KreloadTimeIncrease -= 0.2f;
        }
    }

    internal class ModifyShroomiteHeadgear : ItemOverride
    {
        public override int TargetID => ItemID.ShroomiteHeadgear;
        public override bool DrawingInfo => false;
        public override void UpdateArmorByHead(Player player, Item body, Item legs) => ModifyShroomiteMask.UpdateArmor(player, body, legs);
    }

    internal class ModifyShroomiteHelmet : ItemOverride
    {
        public override int TargetID => ItemID.ShroomiteHelmet;
        public override bool DrawingInfo => false;
        public override void UpdateArmorByHead(Player player, Item body, Item legs) => ModifyShroomiteMask.UpdateArmor(player, body, legs);
    }
}

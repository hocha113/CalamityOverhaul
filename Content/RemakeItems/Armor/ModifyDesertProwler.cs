using CalamityMod.Items.Armor.DesertProwler;
using CalamityOverhaul.Common;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Armor
{
    //荒漠迅游者
    internal class ModifyDesertProwler : CWRItemOverride
    {
        public override int TargetID => ModContent.ItemType<DesertProwlerHat>();
        public override bool CanLoadLocalization => false;
        public override bool DrawingInfo => false;
        public override void UpdateArmorByHead(Player player, Item body, Item legs) => UpdateArmor(player, body, legs);
        public static void UpdateArmor(Player player, Item body, Item legs) {
            if (body.type != ModContent.ItemType<DesertProwlerShirt>() || legs.type != ModContent.ItemType<DesertProwlerPants>()) {
                return;
            }
            player.setBonus += "\n" + CWRLocText.Instance.KreloadTimeLessenText + "4%";
            player.CWR().KreloadTimeIncrease -= 0.04f;
        }
    }
}
